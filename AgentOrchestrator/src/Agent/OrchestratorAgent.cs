using AgentOrchestrator.Constants;
using AgentOrchestrator.Models;
using AgentOrchestrator.Plugins;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.SemanticKernel;
using System.Text.Json;

namespace AgentOrchestrator.Agent;

public class OrchestratorAgent : AgentApplication
{
    private readonly Kernel _kernel;
    //private readonly ITokenService _tokenService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly OrchestrationSettings _orchestrationSettings;
    private readonly ILogger<OrchestratorAgent> _logger;
    private readonly IServiceProvider sp;
    private const int MaxMessageLength = 4000;

    private const string AgenticAuthHandler = "agentic";
    private const string NonAgenticAuthHandler = "me";

    public OrchestratorAgent(
        AgentApplicationOptions options,
        Kernel kernel,
        IHttpContextAccessor httpContextAccessor,
        OrchestrationSettings orchestrationSettings,
        ILogger<OrchestratorAgent> logger,
        IServiceProvider serviceProvider) : base(options)
    {
        sp= serviceProvider;
        _kernel = kernel.Clone();
        _httpContextAccessor = httpContextAccessor;
        _orchestrationSettings = orchestrationSettings;
        _logger = logger;

        // Register activity handlers
        OnActivity(ActivityTypes.ConversationUpdate, OnConversationUpdateAsync);
        OnActivity(ActivityTypes.Message, OnMessageActivityAsync, isAgenticOnly: false, autoSignInHandlers: [NonAgenticAuthHandler]); // Support for OBO flows for users. 
        OnActivity(ActivityTypes.Message, OnMessageActivityAsync, isAgenticOnly: true, autoSignInHandlers: [AgenticAuthHandler]); // Support for AgentID flows for Digital workers. 
    }

    private async Task OnMessageActivityAsync(
        ITurnContext turnContext,
        ITurnState turnState,
        CancellationToken cancellationToken)
    {

        // Set the auth handler to pass to tools based on whether this is an agentic request or not.
        string AuthHandlerName = turnContext.IsAgenticRequest() ? AgenticAuthHandler : NonAgenticAuthHandler;

        var userMessage = turnContext.Activity.Text?.Trim() ?? string.Empty;

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
            
            // Setup the Kernel with tools/plugins in the current conversation context
            UpdateChatClientWithToolsInContext(turnContext, turnState, UserAuthorization, AuthHandlerName);

            // Step 1: Analyze intent
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

            // Step 2: Execute agents based on intents
            _logger.LogInformation("Step 2: Executing agents (parallel={Parallel})...",
                _orchestrationSettings.EnableParallelExecution);
            var responses = await ExecuteAgentsAsync(intents, timeoutToken);

            // Step 3: Synthesize response
            _logger.LogInformation("Step 3: Synthesizing response...");
            var finalResponse = await SynthesizeResponseAsync(userMessage, responses, timeoutToken);

            // Step 4: Send response
            turnContext.StreamingResponse.QueueTextChunk(finalResponse);

            _logger.LogInformation("Response sent successfully");
        }
        catch (UnauthorizedAccessException)
        {
            turnContext.StreamingResponse.QueueTextChunk("Please log in to access M365 features. Visit the web interface to authenticate.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Request timed out after {Seconds} seconds", _orchestrationSettings.TimeoutSeconds);
            turnContext.StreamingResponse.QueueTextChunk("The request timed out. Please try a simpler query or try again later.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message");
            // Don't expose internal error details to users
            turnContext.StreamingResponse.QueueTextChunk("Sorry, an error occurred processing your request. Please try again.");
        }
        finally
        {
            await turnContext.StreamingResponse.EndStreamAsync(cancellationToken);
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
        CancellationToken cancellationToken)
    {
        if (_orchestrationSettings.EnableParallelExecution)
        {
            var tasks = intents.Select(intent => ExecuteAgentForIntentAsync(intent, cancellationToken));
            var responses = await Task.WhenAll(tasks);
            return responses.ToList();
        }
        else
        {
            // Sequential execution
            var responses = new List<AgentResponse>();
            foreach (var intent in intents)
            {
                var response = await ExecuteAgentForIntentAsync(intent, cancellationToken);
                responses.Add(response);
            }
            return responses;
        }
    }

    private async Task<AgentResponse> ExecuteAgentForIntentAsync(
        Intent intent,
        CancellationToken cancellationToken)
    {
        try
        {
            return intent.Type switch
            {
                IntentType.M365Email => await ExecuteM365PluginAsync("QueryEmails", intent.Query, cancellationToken),
                IntentType.M365Calendar => await ExecuteM365PluginAsync("QueryCalendar", intent.Query, cancellationToken),
                IntentType.M365Files => await ExecuteM365PluginAsync("QueryFiles", intent.Query, cancellationToken),
                IntentType.M365People => await ExecuteM365PluginAsync("QueryPeople", intent.Query, cancellationToken),
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
            return new AgentResponse
            {
                Agent = intent.Type.ToString(),
                IntentType = intent.Type,
                Content = $"Error: {ex.Message}",
                Success = false
            };
        }
    }

    private async Task<AgentResponse> ExecuteM365PluginAsync(
        string functionName,
        string query,
        CancellationToken cancellationToken)
    {
        var result = await _kernel.InvokeAsync(
            PluginNames.M365Copilot,
            functionName,
            new()
            {
                ["query"] = query
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
                            "Welcome to the Agent! I can help you with M365 data (emails, calendar, files, people) " +
                            "and general knowledge questions. Try asking something like:\n\n" +
                            "- \"Summarize my emails from today\"\n" +
                            "- \"What meetings do I have tomorrow?\"\n" +
                            "- \"Explain what microservices are\""),
                        cancellationToken);
                }
            }
        }
    }

    /// <summary>
    /// Registers conversation-scoped plugins with the kernel using the current turn context,
    /// state, and user authorization. This enables tools to access <see cref="AgentContext"/>
    /// and operate with the appropriate authentication handler.
    /// </summary>
    /// <param name="turnContext">The current <see cref="ITurnContext"/> for the incoming activity.</param>
    /// <param name="turnState">The mutable <see cref="ITurnState"/> shared across the turn pipeline.</param>
    /// <param name="userAuthorization">The <see cref="UserAuthorization"/> for the active user or agent.</param>
    /// <param name="userAuthHandlerName">The name of the auth handler (e.g., agentic or non-agentic) used for tool calls.</param>
    /// <remarks>
    /// This method wires up the following plugins:
    /// <list type="bullet">
    /// <item>Intent plugin for intent analysis</item>
    /// <item>M365 Copilot plugin for email, calendar, files, and people queries</item>
    /// <item>Azure OpenAI plugin for general knowledge</item>
    /// <item>Synthesis plugin for response aggregation</item>
    /// </list>
    /// Each plugin receives an <see cref="AgentContext"/> instance to access the turn context,
    /// turn state, and authorization data.
    /// </remarks>
    private void UpdateChatClientWithToolsInContext(ITurnContext turnContext, 
        ITurnState turnState, 
        UserAuthorization userAuthorization, 
        string userAuthHandlerName)
    {
        AgentContext agentContext = new(turnContext, turnState, userAuthorization, userAuthHandlerName);

        _kernel.Plugins.AddFromObject(
            new IntentPlugin(
                agentContext, _kernel, 
                sp.GetRequiredService<ILogger<IntentPlugin>>()),
        PluginNames.Intent);

        _kernel.Plugins.AddFromObject(
        new M365CopilotPlugin(
            agentContext,
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<MicrosoftGraphSettings>(),
            sp.GetRequiredService<ILogger<M365CopilotPlugin>>()),
        PluginNames.M365Copilot);

        _kernel.Plugins.AddFromObject(
        new AzureOpenAIPlugin(
            agentContext,
            _kernel, 
            sp.GetRequiredService<ILogger<AzureOpenAIPlugin>>()),
        PluginNames.AzureOpenAI);

        _kernel.Plugins.AddFromObject(
        new SynthesisPlugin(
            agentContext,
            _kernel,
            sp.GetRequiredService<ILogger<SynthesisPlugin>>()),
        PluginNames.Synthesis);
    }
}
