using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace AgentOrchestrator.Plugins;

public class SynthesisPlugin
{
    private readonly Kernel _kernel;
    private readonly ILogger<SynthesisPlugin>? _logger;
    private readonly AgentContext _context;

    public SynthesisPlugin(AgentContext agentContext, Kernel kernel, ILogger<SynthesisPlugin>? logger = null)
    {
        _context = agentContext;
        _kernel = kernel;
        _logger = logger;
    }

    [KernelFunction]
    [Description("Synthesizes multiple agent responses into a coherent unified response")]
    public async Task<string> SynthesizeAsync(
        [Description("The original user query")] string originalQuery,
        [Description("JSON array of agent responses to synthesize")] string responses,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Synthesizing responses for query: {Query}", originalQuery);
        
        await _context.Context.StreamingResponse.QueueInformativeUpdateAsync("Synthesizing responses...");

        var prompt = $""""
            You are a response synthesizer. Your job is to combine multiple agent responses into a single,
            coherent response that addresses the user's original query.

            Original User Query: {originalQuery}

            Agent Responses:
            {responses}

            Instructions:
            1. Analyze all the agent responses
            2. Combine them into a single, well-organized response
            3. Maintain clear structure - if there are multiple topics, organize them with headers or clear transitions
            4. Remove any redundancy between responses
            5. Ensure the response directly addresses the user's original query
            6. Keep the tone helpful and conversational
            7. If one response is about M365 data (emails, calendar, etc.) and another is general knowledge,
               present the M365 data first, then the general information.
            8. Make sure that all responses are represented is represented in Markdown format for better readability.

            """";
        var addtionrules = """"


            """";

        var prompt2 = $""""

            Synthesized Response:
            """";

        prompt += prompt2;
        var result = await _kernel.InvokePromptAsync(prompt, cancellationToken: cancellationToken);
        return result.GetValue<string>() ?? "I couldn't synthesize a response.";
    }
}
