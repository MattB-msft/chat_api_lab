namespace AgentOrchestrator.Auth;

/// <summary>
/// Custom authentication middleware for session-based auth.
///
/// MIDDLEWARE PATTERN:
/// - Middleware intercepts every HTTP request in the pipeline
/// - Can short-circuit the pipeline (return early) or pass to next middleware
/// - Order matters: this runs after UseSession() but before endpoint routing
///
/// SECURITY CONSIDERATIONS:
/// - Public paths are explicitly allowlisted (safe default: deny all)
/// - Session-based auth requires cookies (credentials: 'include' in fetch)
/// - Path matching must be precise to prevent bypasses
/// - Swagger is only allowed in Development environment
/// </summary>
public class AuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuthMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    // SECURITY: Explicit allowlist of public paths (deny by default)
    // Be careful when adding paths - each one bypasses authentication
    private static readonly string[] PublicPaths =
    [
        "/auth/login",
        "/auth/callback",
        "/auth/status",
        "/",
        "/index.html",
        "/css",
        "/js",
        "/api/messages",  // Bot Framework endpoint - has its own JWT auth via M365 Agents SDK
        "/v3/conversations",  // Bot Framework callback endpoint for sending responses
        "/health",        // Health check endpoints for Azure App Service
        "/ready"
    ];

    // Paths only allowed in Development environment
    private static readonly string[] DevelopmentOnlyPaths =
    [
        "/swagger"
    ];

    public AuthMiddleware(RequestDelegate next, ILogger<AuthMiddleware> logger, IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";

        // Block development-only paths in production
        if (!_environment.IsDevelopment() && IsDevelopmentOnlyPath(path))
        {
            _logger.LogWarning("Blocked access to development-only path {Path} in {Environment}", 
                path, _environment.EnvironmentName);
            context.Response.StatusCode = 404;
            return;
        }

        // Allow public paths
        if (IsPublicPath(path))
        {
            await _next(context);
            return;
        }

        // Allow development-only paths in development
        if (_environment.IsDevelopment() && IsDevelopmentOnlyPath(path))
        {
            await _next(context);
            return;
        }

        // Check authentication for API endpoints
        var isAuthenticated = context.Session.GetString("IsAuthenticated") == "true";

        if (!isAuthenticated)
        {
            _logger.LogWarning("Unauthorized access attempt to {Path}", path);
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized. Please login first." });
            return;
        }

        await _next(context);
    }

    /// <summary>
    /// Check if path is a development-only path (e.g., swagger).
    /// </summary>
    private static bool IsDevelopmentOnlyPath(string path)
    {
        foreach (var devPath in DevelopmentOnlyPaths)
        {
            if (path.Equals(devPath, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(devPath + "/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(devPath + ".", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// SECURITY: Path matching for authorization must be precise.
    /// We check for exact match OR path prefix to handle:
    /// - /auth/login (exact public path)
    /// - /css/styles.css (file under public directory)
    /// - /js/chat.js (file under public directory)
    ///
    /// WARNING: Be careful with path matching - attackers may try:
    /// - Path traversal: /css/../api/messages
    /// - URL encoding: /api%2Fmessages
    /// - Case variations: /API/messages
    /// ASP.NET Core normalizes paths, but always validate carefully.
    /// </summary>
    private static bool IsPublicPath(string path)
    {
        foreach (var publicPath in PublicPaths)
        {
            if (path.Equals(publicPath, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(publicPath + "/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(publicPath + ".", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

public static class AuthMiddlewareExtensions
{
    public static IApplicationBuilder UseAuthMiddleware(this IApplicationBuilder app)
    {
        return app.UseMiddleware<AuthMiddleware>();
    }
}
