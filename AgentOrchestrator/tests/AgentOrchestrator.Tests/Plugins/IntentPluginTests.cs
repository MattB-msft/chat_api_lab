using AgentOrchestrator.Plugins;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Moq;
using Xunit;

namespace AgentOrchestrator.Tests.Plugins;

public class IntentPluginTests
{
    [Fact]
    public void Constructor_WithValidKernel_ShouldNotThrow()
    {
        // Arrange
        var kernelBuilder = Kernel.CreateBuilder();
        var kernel = kernelBuilder.Build();
        var agentContext = new Mock<AgentContext>().Object;

        // Act & Assert
        var plugin = new IntentPlugin(agentContext, kernel);
        Assert.NotNull(plugin);
    }

    [Theory]
    [InlineData("```json\n[{\"type\": \"M365Email\"}]\n```", "[{\"type\": \"M365Email\"}]")]
    [InlineData("```\n[{\"type\": \"M365Calendar\"}]\n```", "[{\"type\": \"M365Calendar\"}]")]
    [InlineData("[{\"type\": \"GeneralKnowledge\"}]", "[{\"type\": \"GeneralKnowledge\"}]")]
    public void ExtractJson_WithMarkdownCodeBlocks_ShouldExtractJsonCorrectly(string input, string expected)
    {
        // Use reflection to test private method
        var method = typeof(IntentPlugin).GetMethod("ExtractJson",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);

        // Act
        var result = method.Invoke(null, [input]) as string;

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExtractJson_WithPlainJson_ShouldReturnTrimmed()
    {
        // Arrange
        var input = "  [{\"type\": \"M365Files\"}]  ";
        var expected = "[{\"type\": \"M365Files\"}]";

        var method = typeof(IntentPlugin).GetMethod("ExtractJson",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);

        // Act
        var result = method.Invoke(null, [input]) as string;

        // Assert
        Assert.Equal(expected, result);
    }
}
