using System.Text.Json;
using AgentOrchestrator.Auth;
using AgentOrchestrator.Constants;
using AgentOrchestrator.Models;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.SemanticKernel;

namespace AgentOrchestrator.Agent;

public class OrchestratorAgent : AgentApplication
{
    private readonly Kernel _kernel;
    private readonly ITokenService _tokenService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly OrchestrationSettings _orchestrationSettings;
    private readonly ILogger<OrchestratorAgent> _logger;

    private const int MaxMessageLength = 4000;
    
    /// <summary>
    /// OAuth handler name configured in appsettings.json under AgentApplication:UserAuthorization:Handlers
    /// </summary>
    private const string OAuthHandlerName = "graph";

    public OrchestratorAgent(
        AgentApplicationOptions options,
        Kernel kernel,
        ITokenService tokenService,
        IHttpContextAccessor httpContextAccessor,
        OrchestrationSettings orchestrationSettings,
        ILogger<OrchestratorAgent> logger) : base(options)
    {
        _kernel = kernel;
        _tokenService = tokenService;
        _httpContextAccessor = httpContextAccessor;
        _orchestrationSettings = orchestrationSettings;
        _logger = logger;

        // Register activity handlers
        // Note: We don't use autoSignInHandlers here because we need to handle auth differently
        // for general knowledge queries (no auth needed) vs M365 queries (auth required)
        OnActivity(ActivityTypes.Message, OnMessageActivityAsync);
        OnActivity(ActivityTypes.ConversationUpdate, OnConversationUpdateAsync);
    }

    private async Task OnMessageActivityAsync(
        ITurnContext turnContext,
        ITurnState turnState,
        CancellationToken cancellationToken)
    {
        var activity = turnContext.Activity;
        var userMessage = activity.Text?.Trim() ?? string.Empty;
        
        _logger.LogInformation("=== MESSAGE RECEIVED ===");
        _logger.LogInformation("Channel: {Channel}", activity.ChannelId);
        _logger.LogInformation("From: {From}", activity.From?.Id);
        _logger.LogInformation("Text: {Text}", userMessage);

        if (string.IsNullOrEmpty(userMessage))
        {
            await turnContext.SendActivityAsync(
                MessageFactory.Text("Please enter a message."),
                cancellationToken);
            return;
        }

        // Input validation
        if (userMessage.Length > MaxMessageLength)
        {
            await turnContext.SendActivityAsync(
                MessageFactory.Text($"Message too long. Maximum {MaxMessageLength} characters allowed."),
                cancellationToken);
            return;
        }

        _logger.LogInformation("Processing message: {Message}", userMessage);

        try
        {
            // Create timeout-aware cancellation token
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_orchestrationSettings.TimeoutSeconds));
            var timeoutToken = timeoutCts.Token;

            // Step 1: Analyze intent (before auth so we can allow general queries)
            _logger.LogInformation("Step 1: Analyzing intent...");
            var intents = await AnalyzeIntentAsync(userMessage, timeoutToken);

            // Apply MaxAgentCalls limit
            if (intents.Count > _orchestrationSettings.MaxAgentCalls)
            {
                _logger.LogWarning("Truncating intents from {Count} to {Max}",
                    intents.Count, _orchestrationSettings.MaxAgentCalls);
                intents = intents.Take(_orchestrationSettings.MaxAgentCalls).ToList();
            }

            _logger.LogInformation("Detected {Count} intent(s): {Intents}",
                intents.Count,
                string.Join(", ", intents.Select(i => i.Type)));

            // Check if any M365 intents require authentication
            var hasM365Intent = intents.Any(i =>
                i.Type == IntentType.M365Email ||
                i.Type == IntentType.M365Calendar ||
                i.Type == IntentType.M365Files ||
                i.Type == IntentType.M365People);

            // Get user's access token only if needed for M365 calls
            string? accessToken = null;
            if (hasM365Intent)
            {
                try
                {
                    accessToken = await GetUserAccessTokenAsync(turnContext, timeoutToken);
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogWarning(ex, "Auth failed for M365 intent, falling back to general knowledge");

                    // For Teams, give a helpful message and handle general knowledge intents only
                    var channelId = turnContext.Activity.ChannelId;
                    if (channelId == "msteams")
                    {
                        // Filter to only general knowledge intents
                        var generalIntents = intents.Where(i => i.Type == IntentType.GeneralKnowledge).ToList();

                        if (generalIntents.Count > 0)
                        {
                            // Continue with just general knowledge
                            intents = generalIntents;
                            accessToken = string.Empty; // Not needed for general knowledge
                        }
                        else
                        {
                            // All intents require M365 - user needs to sign in
                            // This should rarely happen since AutoSignIn is enabled
                            await turnContext.SendActivityAsync(
                                MessageFactory.Text(
                                    "To access your M365 data (emails, calendar, files), I need you to sign in first.\n\n" +
                                    "Please click the Sign In button when prompted, or try sending your message again.\n\n" +
                                    "In the meantime, I can answer general knowledge questions. Try:\n" +
                                    "- \"What is machine learning?\"\n" +
                                    "- \"Explain microservices\""),
                                cancellationToken);
                            return;
                        }
                    }
                    else
                    {
                        throw; // Re-throw for web channel
                    }
                }
            }

            // Step 2: Execute agents based on intents
            _logger.LogInformation("Step 2: Executing agents (parallel={Parallel})...",
                _orchestrationSettings.EnableParallelExecution);
            var responses = await ExecuteAgentsAsync(intents, accessToken ?? string.Empty, timeoutToken);

            // Step 3: Synthesize response
            _logger.LogInformation("Step 3: Synthesizing response...");
            var finalResponse = await SynthesizeResponseAsync(userMessage, responses, timeoutToken);

            // Step 4: Send response
            await turnContext.SendActivityAsync(
                MessageFactory.Text(finalResponse),
                cancellationToken);

            _logger.LogInformation("Response sent successfully");
        }
        catch (UnauthorizedAccessException)
        {
            await turnContext.SendActivityAsync(
                MessageFactory.Text("Please log in to access M365 features. Visit the web interface to authenticate."),
                cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Request timed out after {Seconds} seconds", _orchestrationSettings.TimeoutSeconds);
            await turnContext.SendActivityAsync(
                MessageFactory.Text("The request timed out. Please try a simpler query or try again later."),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message");
            // Don't expose internal error details to users
            await turnContext.SendActivityAsync(
                MessageFactory.Text("Sorry, an error occurred processing your request. Please try again."),
                cancellationToken);
        }
    }

    private async Task<List<Intent>> AnalyzeIntentAsync(string query, CancellationToken cancellationToken)
    {
        var result = await _kernel.InvokeAsync(
            PluginNames.Intent,
            "AnalyzeIntent",
            new() { ["query"] = query },
            cancellationToken);

        var json = result.GetValue<string>() ?? "[]";

        try
        {
            var intents = JsonSerializer.Deserialize<List<Intent>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (intents == null || intents.Count == 0)
            {
                _logger.LogWarning("Intent analysis returned empty results for query: {Query}", query);
                return [new Intent { Type = IntentType.GeneralKnowledge, Query = query }];
            }

            return intents;
        }
        catch (JsonException ex)
        {
            // DEBUGGING TIP: Always log the actual response when JSON parsing fails.
            // This helps identify if the LLM is returning unexpected formats.
            _logger.LogWarning(ex, "Failed to parse intent response. Raw JSON: {Json}. Defaulting to general knowledge.", json);
            return [new Intent { Type = IntentType.GeneralKnowledge, Query = query }];
        }
    }

    private async Task<List<AgentResponse>> ExecuteAgentsAsync(
        List<Intent> intents,
        string accessToken,
        CancellationToken cancellationToken)
    {
        if (_orchestrationSettings.EnableParallelExecution)
        {
            var tasks = intents.Select(intent => ExecuteAgentForIntentAsync(intent, accessToken, cancellationToken));
            var responses = await Task.WhenAll(tasks);
            return responses.ToList();
        }
        else
        {
            // Sequential execution
            var responses = new List<AgentResponse>();
            foreach (var intent in intents)
            {
                var response = await ExecuteAgentForIntentAsync(intent, accessToken, cancellationToken);
                responses.Add(response);
            }
            return responses;
        }
    }

    private async Task<AgentResponse> ExecuteAgentForIntentAsync(
        Intent intent,
        string accessToken,
        CancellationToken cancellationToken)
    {
        try
        {
            return intent.Type switch
            {
                IntentType.M365Email => await ExecuteM365PluginAsync("QueryEmails", intent.Query, accessToken, cancellationToken),
                IntentType.M365Calendar => await ExecuteM365PluginAsync("QueryCalendar", intent.Query, accessToken, cancellationToken),
                IntentType.M365Files => await ExecuteM365PluginAsync("QueryFiles", intent.Query, accessToken, cancellationToken),
                IntentType.M365People => await ExecuteM365PluginAsync("QueryPeople", intent.Query, accessToken, cancellationToken),
                IntentType.GeneralKnowledge => await ExecuteGeneralKnowledgeAsync(intent.Query, cancellationToken),
                _ => new AgentResponse
                {
                    Agent = "unknown",
                    IntentType = intent.Type,
                    Content = "I'm not sure how to handle that request.",
                    Success = false
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing agent for intent {IntentType}", intent.Type);
            // SECURITY: Don't expose exception details to users
            return new AgentResponse
            {
                Agent = intent.Type.ToString(),
                IntentType = intent.Type,
                Content = "Sorry, I encountered an issue processing this part of your request.",
                Success = false
            };
        }
    }

    private async Task<AgentResponse> ExecuteM365PluginAsync(
        string functionName,
        string query,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var result = await _kernel.InvokeAsync(
            PluginNames.M365Copilot,
            functionName,
            new()
            {
                ["query"] = query,
                ["accessToken"] = accessToken
            },
            cancellationToken);

        return new AgentResponse
        {
            Agent = "m365_copilot",
            IntentType = functionName switch
            {
                "QueryEmails" => IntentType.M365Email,
                "QueryCalendar" => IntentType.M365Calendar,
                "QueryFiles" => IntentType.M365Files,
                "QueryPeople" => IntentType.M365People,
                _ => IntentType.GeneralKnowledge
            },
            Content = result.GetValue<string>() ?? string.Empty,
            Success = true
        };
    }

    private async Task<AgentResponse> ExecuteGeneralKnowledgeAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var result = await _kernel.InvokeAsync(
            PluginNames.AzureOpenAI,
            "GeneralKnowledge",
            new() { ["query"] = query },
            cancellationToken);

        return new AgentResponse
        {
            Agent = "azure_openai",
            IntentType = IntentType.GeneralKnowledge,
            Content = result.GetValue<string>() ?? string.Empty,
            Success = true
        };
    }

    private async Task<string> SynthesizeResponseAsync(
        string originalQuery,
        List<AgentResponse> responses,
        CancellationToken cancellationToken)
    {
        // If only one successful response, return it directly
        var successfulResponses = responses.Where(r => r.Success).ToList();
        if (successfulResponses.Count == 1)
        {
            return successfulResponses[0].Content;
        }

        if (successfulResponses.Count == 0)
        {
            return "I wasn't able to find an answer to your question.";
        }

        // Multiple responses - synthesize them
        var responsesJson = JsonSerializer.Serialize(successfulResponses.Select(r => new
        {
            agent = r.Agent,
            content = r.Content
        }));

        var result = await _kernel.InvokeAsync(
            PluginNames.Synthesis,
            "Synthesize",
            new()
            {
                ["originalQuery"] = originalQuery,
                ["responses"] = responsesJson
            },
            cancellationToken);

        return result.GetValue<string>() ?? "I couldn't synthesize a response.";
    }

    private async Task<string> GetUserAccessTokenAsync(
        ITurnContext turnContext,
        CancellationToken cancellationToken)
    {
        var channelId = turnContext.Activity.ChannelId;

        // Handle Teams channel with SSO
        if (channelId == "msteams")
        {
            return await GetTeamsSsoTokenAsync(turnContext, cancellationToken);
        }

        // For web/emulator channel, get token from HTTP context session
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            throw new UnauthorizedAccessException("No HTTP context available. Please log in via the web interface.");
        }

        var sessionId = httpContext.Session.Id;
        if (string.IsNullOrEmpty(sessionId))
        {
            throw new UnauthorizedAccessException("No session available. Please log in.");
        }

        try
        {
            var token = await _tokenService.GetAccessTokenAsync(sessionId);
            if (!string.IsNullOrEmpty(token))
            {
                return token;
            }
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Token not found for session {SessionId}", sessionId);
        }

        throw new UnauthorizedAccessException("No access token available. Please log in.");
    }

    /// <summary>
    /// Get access token for Teams users using the M365 Agents SDK UserAuthorization.
    ///
    /// The SDK handles:
    /// 1. Automatic sign-in card display when no token is available
    /// 2. Token caching and refresh
    /// 3. SSO token exchange (when configured)
    ///
    /// Configuration is in appsettings.json under AgentApplication:UserAuthorization
    /// See: https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/agent-oauth-configuration-dotnet
    /// </summary>
    private async Task<string> GetTeamsSsoTokenAsync(
        ITurnContext turnContext,
        CancellationToken cancellationToken)
    {
        var activity = turnContext.Activity;
        var userId = activity.From?.Id ?? "unknown";

        _logger.LogInformation("Getting token for Teams user {UserId} via SDK UserAuthorization", userId);

        try
        {
            // Use the SDK's built-in UserAuthorization to get token
            // This automatically handles sign-in cards, token exchange, and caching
            var token = await UserAuthorization.GetTurnTokenAsync(turnContext, OAuthHandlerName);
            
            if (!string.IsNullOrEmpty(token))
            {
                _logger.LogInformation("Token retrieved successfully for Teams user {UserId}", userId);
                return token;
            }
            
            _logger.LogWarning("GetTurnTokenAsync returned null/empty for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get token via SDK for user {UserId}", userId);
        }

        // If SDK didn't return a token, sign-in is required
        // The SDK should have already sent a sign-in card, but we'll throw to stop processing
        throw new UnauthorizedAccessException(
            "Please sign in using the button above to access your M365 data.");
    }

    private async Task OnConversationUpdateAsync(
        ITurnContext turnContext,
        ITurnState turnState,
        CancellationToken cancellationToken)
    {
        var membersAdded = turnContext.Activity.MembersAdded;
        if (membersAdded != null)
        {
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(
                        MessageFactory.Text(
                            "Welcome to the .NET 10 Agent! I can help you with M365 data (emails, calendar, files, people) " +
                            "and general knowledge questions. Try asking something like:\n\n" +
                            "- \"Summarize my emails from today\"\n" +
                            "- \"What meetings do I have tomorrow?\"\n" +
                            "- \"Explain what microservices are\""),
                        cancellationToken);
                }
            }
        }
    }
}
