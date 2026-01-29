using AgentOrchestrator.Models;
using Xunit;

namespace AgentOrchestrator.Tests.Exercises;

/// <summary>
/// LAB EXERCISE: Complete these tests to verify your understanding.
///
/// Instructions:
/// 1. Read each test's description and TODO comment
/// 2. Implement the test logic to make the test pass
/// 3. Run tests with: dotnet test
///
/// These exercises reinforce key concepts from the lab.
/// </summary>
public class StudentExerciseTests
{
    // ========================================================================
    // EXERCISE 1: Intent Classification
    // ========================================================================
    // Understanding how the IntentPlugin classifies user queries is essential.
    // The Intent model has a Type (enum) and a Query (string).

    [Fact]
    public void Exercise1_Intent_ShouldIdentifyM365EmailIntent()
    {
        // TODO: Create an Intent object with Type = IntentType.M365Email
        // and verify IsM365Intent returns true

        // Arrange
        var intent = new Intent
        {
            Type = IntentType.M365Email,
            Query = "Summarize my emails"
        };

        // Act & Assert
        Assert.True(intent.IsM365Intent, "M365Email should be identified as an M365 intent");
    }

    [Fact]
    public void Exercise1_Intent_GeneralKnowledge_ShouldNotBeM365Intent()
    {
        // TODO: Create an Intent with Type = IntentType.GeneralKnowledge
        // and verify IsM365Intent returns false

        // Arrange - YOUR CODE HERE
        // Intent intent = null!; // Replace with actual Intent creation

        // Act & Assert
        // Uncomment the assertion below when you've created the intent
        // Assert.False(intent.IsM365Intent, "GeneralKnowledge should NOT be an M365 intent");

        // Remove this line when you implement the test
        Assert.True(true, "TODO: Implement this test");
    }

    // ========================================================================
    // EXERCISE 2: Configuration Validation
    // ========================================================================
    // Understanding configuration is important for deployment.

    [Fact]
    public void Exercise2_OrchestrationSettings_ShouldHaveReasonableDefaults()
    {
        // TODO: Verify that OrchestrationSettings has sensible default values
        // for MaxAgentCalls, TimeoutSeconds, and EnableParallelExecution

        // Arrange
        var settings = new OrchestrationSettings();

        // Act & Assert
        // What should MaxAgentCalls be? (Hint: check appsettings.json)
        Assert.True(settings.MaxAgentCalls > 0, "MaxAgentCalls should be positive");
        Assert.True(settings.MaxAgentCalls <= 10, "MaxAgentCalls should be reasonable");

        // TODO: Add assertion for TimeoutSeconds
        // What's a reasonable timeout for API calls that can take 10-30 seconds?

        // TODO: Add assertion for EnableParallelExecution
        // Should parallel execution be enabled by default?
    }

    // ========================================================================
    // EXERCISE 3: AgentResponse Model
    // ========================================================================
    // Understanding the response model helps with debugging.

    [Fact]
    public void Exercise3_AgentResponse_ShouldTrackSuccessState()
    {
        // TODO: Create AgentResponse objects for success and failure cases
        // and verify the Success property works correctly

        // Successful response
        var successResponse = new AgentResponse
        {
            Agent = "m365_copilot",
            IntentType = IntentType.M365Email,
            Content = "You have 5 unread emails...",
            Success = true
        };

        Assert.True(successResponse.Success);
        Assert.NotEmpty(successResponse.Content);

        // TODO: Create a failed response and assert Success is false
        // Hint: What would happen if the Copilot API returned an error?
    }

    // ========================================================================
    // EXERCISE 4: Understanding Scopes
    // ========================================================================
    // API permissions (scopes) are critical for M365 integration.

    [Theory]
    [InlineData("Mail.Read", true)]
    [InlineData("Calendars.Read", true)]
    [InlineData("Files.Read.All", true)]
    [InlineData("Mail.Send", false)]  // We only READ emails, not send
    [InlineData("User.ReadWrite", false)]  // We only READ user info
    public void Exercise4_UnderstandingScopes_ReadOnlyAccess(string scope, bool shouldBeIncluded)
    {
        // This test verifies understanding of the principle of least privilege.
        // Our lab only needs READ permissions, not WRITE.

        // TODO: Review the scopes in appsettings.json and understand why
        // we use Mail.Read instead of Mail.ReadWrite

        // The lab uses these scopes for Copilot Chat API:
        var labScopes = new[]
        {
            "openid", "profile", "email", "User.Read",
            "Mail.Read", "Calendars.Read", "Files.Read.All",
            "Sites.Read.All", "People.Read.All", "Chat.Read",
            "OnlineMeetingTranscript.Read.All", "ChannelMessage.Read.All",
            "ExternalItem.Read.All"
        };

        var isIncluded = labScopes.Contains(scope);

        // Verify our understanding matches reality
        Assert.Equal(shouldBeIncluded, isIncluded);
    }

    // ========================================================================
    // BONUS EXERCISE: Error Handling
    // ========================================================================
    // Understanding error handling is important for robust applications.

    [Fact]
    public void BonusExercise_TokenService_ThrowsWhenNoToken()
    {
        // This test is already implemented in TokenServiceTests.cs
        // Review it to understand how the token service handles missing tokens.

        // Question: What exception type is thrown when no token is found?
        // Answer: InvalidOperationException

        // Question: Why is this better than returning null?
        // Answer: Explicit errors are easier to debug than null reference exceptions

        Assert.True(true, "Review TokenServiceTests.cs for the implementation");
    }
}
