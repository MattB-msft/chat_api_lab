using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace AgentOrchestrator.Plugins;

public class IntentPlugin
{
    private readonly Kernel _kernel;
    private readonly ILogger<IntentPlugin>? _logger;
    private readonly AgentContext _context;

    public IntentPlugin(AgentContext agentContext, Kernel kernel, ILogger<IntentPlugin>? logger = null)
    {
        _context = agentContext;
        _kernel = kernel;
        _logger = logger;
    }

    [KernelFunction]
    [Description("Analyzes a user query to identify intent types for routing to appropriate agents")]
    public async Task<string> AnalyzeIntentAsync(
        [Description("The user's query to analyze")] string query,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Analyzing intent for query: {Query}", query);
        
        await _context.Context.StreamingResponse.QueueInformativeUpdateAsync("Analyzing intent...");

        var prompt = $$"""
            You are an intent classifier for a multi-agent system. Analyze the user's query and identify which agents should handle it.

            Available intent types:
            - M365Email: Questions about emails, messages, inbox, mail
            - M365Calendar: Questions about meetings, schedule, calendar, appointments
            - M365Files: Questions about documents, files, SharePoint, OneDrive
            - M365People: Questions about colleagues, organization, team members, expertise
            - GeneralKnowledge: General questions not related to Microsoft 365 data

            Rules:
            1. A query can have multiple intents (e.g., "Summarize my emails and explain REST APIs" has M365Email + GeneralKnowledge)
            2. If the query mentions personal data (my emails, my calendar, my files, my team), route to the appropriate M365 intent
            3. If the query is about general concepts, technology, or information not in M365, use GeneralKnowledge
            4. Extract the relevant sub-query for each intent

            User Query: {{query}}

            Respond with ONLY a JSON array, no other text:
            [
              {"type": "IntentType", "query": "extracted sub-query for this intent"}
            ]

            Example for "What meetings do I have tomorrow and what is Docker?":
            [
              {"type": "M365Calendar", "query": "What meetings do I have tomorrow"},
              {"type": "GeneralKnowledge", "query": "What is Docker"}
            ]
            """;

        var result = await _kernel.InvokePromptAsync(prompt, cancellationToken: cancellationToken);
        var response = result.GetValue<string>() ?? "[]";

        // Clean up response - extract JSON if wrapped in markdown
        response = ExtractJson(response);

        return response;
    }

    private static string ExtractJson(string response)
    {
        // Remove markdown code blocks if present
        if (response.Contains("```json"))
        {
            var start = response.IndexOf("```json") + 7;
            var end = response.LastIndexOf("```");
            if (end > start)
            {
                response = response[start..end];
            }
        }
        else if (response.Contains("```"))
        {
            var start = response.IndexOf("```") + 3;
            var end = response.LastIndexOf("```");
            if (end > start)
            {
                response = response[start..end];
            }
        }

        return response.Trim();
    }
}
