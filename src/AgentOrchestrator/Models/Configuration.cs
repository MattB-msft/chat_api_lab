namespace AgentOrchestrator.Models;

public class AzureAdSettings
{
    public string Instance { get; set; } = "https://login.microsoftonline.com/";
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string CallbackPath { get; set; } = "/auth/callback";
    public string RedirectUri { get; set; } = "http://localhost:5000/auth/callback";
    public List<string> Scopes { get; set; } = [];
}

public class AzureOpenAISettings
{
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = "gpt-4o";
    public string ApiVersion { get; set; } = "2024-08-01-preview";
}

public class MicrosoftGraphSettings
{
    public string BaseUrl { get; set; } = "https://graph.microsoft.com/beta";
    public string CopilotChatEndpoint { get; set; } = "/copilot/conversations";
}

public class OrchestrationSettings
{
    public int MaxAgentCalls { get; set; } = 5;
    public int TimeoutSeconds { get; set; } = 30;
    public bool EnableParallelExecution { get; set; } = true;
}
