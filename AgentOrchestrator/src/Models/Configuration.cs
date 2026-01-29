namespace AgentOrchestrator.Models;

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
    public string CopilotChatEndpoint { get; set; } = "/me/copilot/chats";
}

public class OrchestrationSettings
{
    public int MaxAgentCalls { get; set; } = 5;
    public int TimeoutSeconds { get; set; } = 30;
    public bool EnableParallelExecution { get; set; } = true;
}
