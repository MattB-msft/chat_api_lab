using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;

namespace AgentOrchestrator.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/auth");

        group.MapGet("/login", Login);
        group.MapGet("/callback", Callback);
        group.MapPost("/logout", Logout);
        group.MapGet("/status", Status);
    }

    private static async Task<IResult> Login(
        HttpContext context,
        ITokenService tokenService)
    {
        // Use framework session ID for consistency with OrchestratorAgent
        // This ensures token storage and retrieval use the same key
        var sessionId = context.Session.Id;

        // Build authorization URL with cryptographically secure state parameter for CSRF protection
        var stateBytes = RandomNumberGenerator.GetBytes(32);
        var state = Convert.ToBase64String(stateBytes);
        context.Session.SetString("AuthState", state);

        var authUrl = await tokenService.BuildAuthorizationUrlAsync(state);

        return Results.Redirect(authUrl);
    }

    private static async Task<IResult> Callback(
        HttpContext context,
        ITokenService tokenService,
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        [FromQuery(Name = "error_description")] string? errorDescription)
    {
        // Handle error from identity provider
        if (!string.IsNullOrEmpty(error))
        {
            return Results.BadRequest(new { error, description = errorDescription });
        }

        // Validate code
        if (string.IsNullOrEmpty(code))
        {
            return Results.BadRequest(new { error = "No authorization code received" });
        }

        // Validate state for CSRF protection
        var expectedState = context.Session.GetString("AuthState");
        if (state != expectedState)
        {
            return Results.BadRequest(new { error = "Invalid state parameter" });
        }

        // Use framework session ID for consistency with OrchestratorAgent
        var sessionId = context.Session.Id;

        try
        {
            var authResult = await tokenService.AcquireTokenByAuthorizationCodeAsync(code, sessionId);

            // Store user info in session
            context.Session.SetString("UserName", authResult.Account?.Username ?? "Unknown");
            context.Session.SetString("IsAuthenticated", "true");

            // Clear auth state
            context.Session.Remove("AuthState");

            // Redirect to home page
            return Results.Redirect("/");
        }
        catch (Exception ex)
        {
            // SECURITY: Never expose exception details to clients.
            // Exception messages can reveal file paths, library versions, database schemas,
            // and other information useful to attackers.
            // Always log full details server-side, return generic message to client.
            var loggerFactory = context.RequestServices.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("AuthEndpoints");
            logger.LogError(ex, "Token acquisition failed during OAuth callback");
            return Results.BadRequest(new { error = "Authentication failed. Please try again." });
        }
    }

    private static IResult Logout(
        HttpContext context,
        ITokenService tokenService)
    {
        // Use framework session ID for consistency
        var sessionId = context.Session.Id;
        tokenService.ClearTokenCache(sessionId);

        context.Session.Clear();

        return Results.Ok(new { message = "Logged out successfully" });
    }

    private static IResult Status(HttpContext context)
    {
        var isAuthenticated = context.Session.GetString("IsAuthenticated") == "true";
        var userName = context.Session.GetString("UserName");

        return Results.Ok(new
        {
            isAuthenticated,
            userName = isAuthenticated ? userName : null
        });
    }
}
