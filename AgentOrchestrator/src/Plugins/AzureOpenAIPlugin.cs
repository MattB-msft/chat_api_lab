using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace AgentOrchestrator.Plugins;

public class AzureOpenAIPlugin
{
    private readonly Kernel _kernel;
    private readonly ILogger<AzureOpenAIPlugin>? _logger;
    private readonly AgentContext _context;

    public AzureOpenAIPlugin(AgentContext agentContext, Kernel kernel, ILogger<AzureOpenAIPlugin>? logger = null)
    {
        _context = agentContext;
        _kernel = kernel;
        _logger = logger;
    }

    [KernelFunction]
    [Description("Answers general knowledge questions that are not related to Microsoft 365 data")]
    public async Task<string> GeneralKnowledgeAsync(
        [Description("The general knowledge question to answer")] string query,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Processing general knowledge query: {Query}", query);
        await _context.Context.StreamingResponse.QueueInformativeUpdateAsync("Contacting Azure OpenAI...");
        var prompt = $"""
            You are a helpful AI assistant. Answer the following question clearly and concisely.

            Question: {query}

            Provide a helpful, accurate, and informative response. If the question is about a technical topic,
            include relevant details and examples where appropriate.
            """;

        var result = await _kernel.InvokePromptAsync(prompt, cancellationToken: cancellationToken);
        return result.GetValue<string>() ?? "I couldn't generate a response.";
    }
}
