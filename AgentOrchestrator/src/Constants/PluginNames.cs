namespace AgentOrchestrator.Constants;

/// <summary>
/// Constants for Semantic Kernel plugin names.
///
/// BEST PRACTICE: Use constants instead of magic strings for plugin names.
/// This prevents typos and makes refactoring easier - change the name in one place
/// and the compiler will catch any mismatches.
/// </summary>
public static class PluginNames
{
    /// <summary>
    /// Intent analysis plugin - classifies user queries into intent categories
    /// </summary>
    public const string Intent = "IntentPlugin";

    /// <summary>
    /// Azure OpenAI plugin - handles general knowledge queries
    /// </summary>
    public const string AzureOpenAI = "AzureOpenAIPlugin";

    /// <summary>
    /// M365 Copilot plugin - handles Microsoft 365 data queries via Chat API
    /// </summary>
    public const string M365Copilot = "M365CopilotPlugin";

    /// <summary>
    /// Synthesis plugin - combines multiple agent responses into coherent answer
    /// </summary>
    public const string Synthesis = "SynthesisPlugin";
}
