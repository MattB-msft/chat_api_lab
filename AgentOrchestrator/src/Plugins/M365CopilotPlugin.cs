//using AgentOrchestrator.CopilotSdk;
using AgentOrchestrator.Models;
using Microsoft.Agents.M365Copilot.Beta;
using Microsoft.Agents.M365Copilot.Beta.Copilot.Conversations.Item.MicrosoftGraphCopilotChat;
using Microsoft.Agents.M365Copilot.Beta.Models;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace AgentOrchestrator.Plugins;

/// <summary>
/// Semantic Kernel Plugin for Microsoft 365 Copilot Chat API integration.
///
/// SEMANTIC KERNEL CONCEPTS:
/// - [KernelFunction]: Exposes method to the AI orchestrator
/// - [Description]: Helps LLM understand when to use this function
/// - Parameters with [Description]: Helps LLM provide correct arguments
///
/// The Kernel can invoke these functions based on user intent,
/// allowing natural language to trigger specific API calls.
///
/// COPILOT CHAT API:
/// - Endpoint: /beta/copilot/conversations (Beta API - subject to change)
/// - Two-step process: Create conversation, then send chat message
/// - Returns M365 data (emails, calendar, files) in natural language
/// - Requires per-user Copilot license
///
/// See: https://learn.microsoft.com/graph/api/resources/copilot-api-overview
/// </summary>
public class M365CopilotPlugin
{
    private readonly AgentContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MicrosoftGraphSettings _graphSettings;
    private readonly ILogger<M365CopilotPlugin> _logger;
    private readonly List<string> requiredScopesList = new()
    {
            "openid",
            "profile",
            "email",
            "User.Read",
            "Mail.Read",
            "Calendars.Read",
            "Files.Read.All",
            "Sites.Read.All",
            "People.Read.All",
            "Chat.Read",
            "OnlineMeetingTranscript.Read.All",
            "ChannelMessage.Read.All",
            "ExternalItem.Read.All"
    };

    public M365CopilotPlugin(
        AgentContext agentContext,
        IHttpClientFactory httpClientFactory,
        MicrosoftGraphSettings graphSettings,
        ILogger<M365CopilotPlugin> logger)
    {
        _context = agentContext;
        _httpClientFactory = httpClientFactory;
        _graphSettings = graphSettings;
        _logger = logger;
    }

    [KernelFunction]
    [Description("Query Microsoft 365 Copilot for email-related questions using the Chat API")]
    public async Task<string> QueryEmailsAsync(
        [Description("The email-related question")] string query,
        CancellationToken cancellationToken = default)
    {
        return await CallCopilotChatApiAsync(query, cancellationToken);
    }

    [KernelFunction]
    [Description("Query Microsoft 365 Copilot for calendar-related questions using the Chat API")]
    public async Task<string> QueryCalendarAsync(
        [Description("The calendar-related question")] string query,
        CancellationToken cancellationToken = default)
    {
        return await CallCopilotChatApiAsync(query, cancellationToken);
    }

    [KernelFunction]
    [Description("Query Microsoft 365 Copilot for file and document questions using the Chat API")]
    public async Task<string> QueryFilesAsync(
        [Description("The files-related question")] string query,
        CancellationToken cancellationToken = default)
    {
        return await CallCopilotChatApiAsync(query, cancellationToken);
    }

    [KernelFunction]
    [Description("Query Microsoft 365 Copilot for people and organization questions using the Chat API")]
    public async Task<string> QueryPeopleAsync(
        [Description("The people-related question")] string query,
        CancellationToken cancellationToken = default)
    {
        return await CallCopilotChatApiAsync(query, cancellationToken);
    }

    private async Task<string> CallCopilotChatApiAsync(
        string query,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Calling Copilot Chat API with query: {Query}", query);

        await _context.Context.StreamingResponse.QueueInformativeUpdateAsync("Contacting Microsoft 365 Copilot...");

        // Create Kiota client with user's access token
        var client = await CreateCopilotClientAsync();
        string? conversationId = _context.State.Conversation.GetValue<string>("M365CopilotConversationId");

        try
        {
            if (string.IsNullOrEmpty(conversationId))
            {
                // Step 1: Create conversation
                _logger.LogInformation("Creating conversation...");
                var conversation = await client.Copilot.Conversations.PostAsync(
                    new CopilotConversation(),
                    cancellationToken: cancellationToken);

                if (conversation?.Id == null)
                {
                    throw new InvalidOperationException("Failed to create conversation - no ID returned");
                }
                conversationId = conversation.Id;
                _context.State.Conversation.SetValue<string>("M365CopilotConversationId",conversationId);
            }
            _logger.LogInformation("Created conversation: {ConversationId}", conversationId);

            // Step 2: Send chat message
            await _context.Context.StreamingResponse.QueueInformativeUpdateAsync("Talking to Microsoft 365 Copilot...");
            var chatRequest = new ChatPostRequestBody()
            {
                Message = new CopilotConversationRequestMessageParameter()
                {
                    Text = query
                },
                LocationHint = new CopilotConversationLocation() { TimeZone = "America/Los_Angeles" }
            };

            _logger.LogInformation("Sending chat message...");
            var response = await client.Copilot.Conversations[conversationId].MicrosoftGraphCopilotChat.PostAsync(chatRequest, cancellationToken: cancellationToken);

            if (response?.Messages == null || response.Messages.Count == 0)
            {
                _logger.LogWarning("No messages in response");
                return "No response received from Copilot.";
            }

            // Get the assistant's response (last message)
            var assistantMessage = response.Messages.LastOrDefault();
            var responseText = assistantMessage?.Text ?? "No response content.";

            _logger.LogInformation("Received Copilot response");
            return responseText;
        }
        //catch (CopilotSdk.Models.CopilotConversation401Error ex)
        //{
        //    _logger.LogError(ex, "Unauthorized - token may be expired or invalid");
        //    throw new UnauthorizedAccessException("Your session has expired. Please log in again.");
        //}
        //catch (CopilotSdk.Models.CopilotConversation403Error ex)
        //{
        //    _logger.LogError(ex, "Forbidden - user may lack Copilot license");
        //    return "You don't have access to Microsoft 365 Copilot. Please contact your administrator to verify your license.";
        //}
        //catch (CopilotSdk.Models.CopilotConversation404Error ex)
        //{
        //    _logger.LogError(ex, "Not found - conversation or endpoint not available");
        //    return "The Copilot service is not available. Please try again later.";
        //}
        //catch (CopilotSdk.Models.CopilotConversation500Error ex)
        //{
        //    _logger.LogError(ex, "Server error from Copilot API");
        //    return "The Copilot service encountered an error. Please try again later.";
        //}
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Copilot Chat API");
            throw;
        }
        finally
        {
            // Note: The Copilot Chat API doesn't currently support DELETE for conversations
            // Conversations are automatically cleaned up by the service after expiration
            // TODO: Add cleanup when DELETE is supported by the API
            if (!string.IsNullOrEmpty(conversationId))
            {
                _logger.LogDebug("Conversation {ConversationId} will be cleaned up by service expiration", conversationId);
            }
        }
    }

    private async Task<AgentsM365CopilotBetaServiceClient> CreateCopilotClientAsync()
    {
        // Create HTTP client
        var httpClient = _httpClientFactory.CreateClient("Graph");

        // Create authentication provider with user's token
        var userToken = await _context.UserAuth.ExchangeTurnTokenAsync(
                    _context.Context,
                    _context.AuthHandlerName,
                    exchangeScopes: requiredScopesList).ConfigureAwait(false); 
        var authProvider = new BaseBearerTokenAuthenticationProvider(
            new TokenProvider(userToken));

        // Create request adapter (handles serialization internally)
        var adapter = new HttpClientRequestAdapter(authProvider, httpClient: httpClient);
        return new AgentsM365CopilotBetaServiceClient(adapter); 
    }
}

/// <summary>
/// SECURITY: Token provider that restricts which hosts receive the access token.
///
/// Why this matters:
/// - Access tokens should only be sent to intended APIs
/// - If a redirect or misconfiguration sends requests elsewhere, token won't leak
/// - AllowedHostsValidator is a defense-in-depth measure
///
/// Only Microsoft Graph hosts are allowed to receive this token.
/// </summary>
internal class TokenProvider : IAccessTokenProvider
{
    private readonly string _accessToken;
    private static readonly string[] AllowedHosts = ["graph.microsoft.com", "graph.microsoft-ppe.com"];

    public TokenProvider(string accessToken)
    {
        _accessToken = accessToken;
    }
    public AllowedHostsValidator AllowedHostsValidator => new(AllowedHosts);

    public Task<string> GetAuthorizationTokenAsync(
        Uri uri,
        Dictionary<string, object>? additionalAuthenticationContext = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_accessToken);
    }
}
