using System.Text.Json.Serialization;

namespace AgentOrchestrator.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IntentType
{
    M365Email,
    M365Calendar,
    M365Files,
    M365People,
    GeneralKnowledge
}

public class Intent
{
    [JsonPropertyName("type")]
    public IntentType Type { get; set; }

    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public float Confidence { get; set; } = 1.0f;

    public bool IsM365Intent => Type switch
    {
        IntentType.M365Email => true,
        IntentType.M365Calendar => true,
        IntentType.M365Files => true,
        IntentType.M365People => true,
        _ => false
    };
}

public record IntentAnalysisResult(
    List<Intent> Intents,
    string OriginalQuery
);
