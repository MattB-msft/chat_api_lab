using AgentOrchestrator.Models;
using System.Text.Json;
using Xunit;

namespace AgentOrchestrator.Tests.Agent;

public class OrchestratorAgentTests
{
    [Fact]
    public void Intent_Deserialization_ShouldWorkCorrectly()
    {
        // Arrange
        var json = """
            [
                {"type": "M365Email", "query": "summarize my emails"},
                {"type": "GeneralKnowledge", "query": "what is Docker"}
            ]
            """;

        // Act
        var intents = JsonSerializer.Deserialize<List<Intent>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        Assert.NotNull(intents);
        Assert.Equal(2, intents.Count);
        Assert.Equal(IntentType.M365Email, intents[0].Type);
        Assert.Equal("summarize my emails", intents[0].Query);
        Assert.Equal(IntentType.GeneralKnowledge, intents[1].Type);
    }

    [Fact]
    public void Intent_IsM365Intent_ShouldReturnCorrectly()
    {
        // Arrange & Act & Assert
        Assert.True(new Intent { Type = IntentType.M365Email }.IsM365Intent);
        Assert.True(new Intent { Type = IntentType.M365Calendar }.IsM365Intent);
        Assert.True(new Intent { Type = IntentType.M365Files }.IsM365Intent);
        Assert.True(new Intent { Type = IntentType.M365People }.IsM365Intent);
        Assert.False(new Intent { Type = IntentType.GeneralKnowledge }.IsM365Intent);
    }

    [Fact]
    public void AgentResponse_Serialization_ShouldWorkCorrectly()
    {
        // Arrange
        var response = new AgentResponse
        {
            Agent = "azure_openai",
            IntentType = IntentType.GeneralKnowledge,
            Content = "Docker is a containerization platform...",
            Success = true
        };

        // Act
        var json = JsonSerializer.Serialize(response);
        var deserialized = JsonSerializer.Deserialize<AgentResponse>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("azure_openai", deserialized.Agent);
        Assert.Equal(IntentType.GeneralKnowledge, deserialized.IntentType);
        Assert.True(deserialized.Success);
    }

    [Fact]
    public void OrchestrationSettings_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var settings = new OrchestrationSettings();

        // Assert
        Assert.Equal(5, settings.MaxAgentCalls);
        // NOTE: Default is 30s but lab config uses 120s for Copilot API latency
        // This test verifies the model default, not the configured value
        Assert.Equal(30, settings.TimeoutSeconds);
        Assert.True(settings.EnableParallelExecution);
    }
}
