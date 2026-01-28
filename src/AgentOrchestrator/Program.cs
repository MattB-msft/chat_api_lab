using System.Threading.RateLimiting;
using AgentOrchestrator.Agent;
using AgentOrchestrator.Auth;
using AgentOrchestrator.Constants;
using AgentOrchestrator.Models;
using AgentOrchestrator.Plugins;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.SemanticKernel;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// CONFIGURATION
// ============================================================================
// SECURITY: Secrets (ClientSecret, ApiKey) should be loaded from:
// - Development: dotnet user-secrets (see appsettings.Development.json.template)
// - Production: Azure Key Vault or environment variables
// Never commit secrets to version control!
// ============================================================================

// === Load Configuration ===
var azureAdSettings = builder.Configuration.GetSection("AzureAd").Get<AzureAdSettings>()
    ?? throw new InvalidOperationException("AzureAd configuration is required");

var azureOpenAISettings = builder.Configuration.GetSection("AzureOpenAI").Get<AzureOpenAISettings>()
    ?? throw new InvalidOperationException("AzureOpenAI configuration is required");

var graphSettings = builder.Configuration.GetSection("MicrosoftGraph").Get<MicrosoftGraphSettings>()
    ?? throw new InvalidOperationException("MicrosoftGraph configuration is required");

var orchestrationSettings = builder.Configuration.GetSection("Orchestration").Get<OrchestrationSettings>()
    ?? new OrchestrationSettings();

// Register configuration as singletons
builder.Services.AddSingleton(azureAdSettings);
builder.Services.AddSingleton(azureOpenAISettings);
builder.Services.AddSingleton(graphSettings);
builder.Services.AddSingleton(orchestrationSettings);

// ============================================================================
// SESSION MANAGEMENT
// ============================================================================
// LAB SIMPLIFICATION: Using in-memory session storage for single-instance development.
//
// PRODUCTION requirements for multi-instance deployments:
// - Redis: builder.Services.AddStackExchangeRedisCache(options => { options.Configuration = "..."; });
// - SQL Server: builder.Services.AddDistributedSqlServerCache(options => { ... });
// - Azure Cache for Redis is recommended for cloud deployments
//
// Without distributed cache, sessions are lost on app restart and users must re-login.
// ============================================================================
builder.Services.AddDistributedMemoryCache(); // Single-instance only
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(1);
    options.Cookie.Name = ".AgentOrchestrator.Session";

    // SECURITY: HttpOnly prevents JavaScript access to session cookie
    // This mitigates XSS attacks that try to steal session tokens
    options.Cookie.HttpOnly = true;

    options.Cookie.IsEssential = true;

    // SECURITY: SameSite prevents CSRF by not sending cookie on cross-site requests
    // Lax: Cookie sent on top-level navigation (GET) but not on cross-site POST
    // This is required for OAuth redirect flow to work properly
    options.Cookie.SameSite = SameSiteMode.Lax;

    // SECURITY: Secure cookie policy for HTTPS (required in production)
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

builder.Services.AddHttpContextAccessor();

// === Auth Services ===
builder.Services.AddSingleton<ITokenService, TokenService>();

// ============================================================================
// HTTP CLIENT WITH RESILIENCE
// ============================================================================
// RELIABILITY: Standard resilience handler adds multiple protection layers:
//
// 1. Retry: Automatically retry failed requests (handles transient failures)
// 2. Circuit Breaker: Stop calling failing services (prevents cascade failures)
// 3. Timeout: Limit how long to wait (prevents resource exhaustion)
//
// These patterns are essential for production cloud applications.
// See: https://learn.microsoft.com/dotnet/core/resilience
// ============================================================================
builder.Services.AddHttpClient("Graph")
    .AddStandardResilienceHandler(options =>
    {
        // Retry up to 3 times with 1 second delay between attempts
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.Delay = TimeSpan.FromSeconds(1);

        // Circuit breaker: If requests fail repeatedly, stop trying for a period
        // SamplingDuration must be >= 2x AttemptTimeout per library requirements
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(240);

        // Copilot API can take 10-30 seconds to respond
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(120);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
    });

// === Semantic Kernel Setup ===
builder.Services.AddSingleton<Kernel>(sp =>
{
    var kernelBuilder = Kernel.CreateBuilder();

    kernelBuilder.AddAzureOpenAIChatCompletion(
        deploymentName: azureOpenAISettings.DeploymentName,
        endpoint: azureOpenAISettings.Endpoint,
        apiKey: azureOpenAISettings.ApiKey
    );

    // Build kernel
    var kernel = kernelBuilder.Build();

    // Get logger factory for plugin logging
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

    // Register plugins with logging
    // BEST PRACTICE: Use constants for plugin names to prevent typos
    kernel.Plugins.AddFromObject(
        new IntentPlugin(kernel, loggerFactory.CreateLogger<IntentPlugin>()),
        PluginNames.Intent);

    kernel.Plugins.AddFromObject(
        new AzureOpenAIPlugin(kernel, loggerFactory.CreateLogger<AzureOpenAIPlugin>()),
        PluginNames.AzureOpenAI);

    kernel.Plugins.AddFromObject(
        new M365CopilotPlugin(
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<MicrosoftGraphSettings>(),
            sp.GetRequiredService<ILogger<M365CopilotPlugin>>()),
        PluginNames.M365Copilot);

    kernel.Plugins.AddFromObject(
        new SynthesisPlugin(kernel, loggerFactory.CreateLogger<SynthesisPlugin>()),
        PluginNames.Synthesis);

    return kernel;
});

// === M365 Agents SDK Setup ===
// LAB SIMPLIFICATION: MemoryStorage stores conversation state in-memory.
// State is lost on app restart. For production, implement IStorage with
// Azure Cosmos DB, SQL Server, or Azure Blob Storage.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

// Add AgentApplicationOptions from configuration
builder.AddAgentApplicationOptions();

// Register the agent
builder.AddAgent<OrchestratorAgent>();

// === CORS Configuration ===
// SECURITY: Configure CORS with least privilege principle
// - Specify exact methods needed (not AllowAnyMethod)
// - Specify exact headers needed (not AllowAnyHeader)
// - Be cautious with AllowCredentials (enables cookie-based requests)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:5000"];
        policy.WithOrigins(allowedOrigins)
              .WithMethods("GET", "POST")           // Only methods we actually need
              .WithHeaders("Content-Type")          // Only headers we actually need
              .AllowCredentials();
    });
});

// ============================================================================
// RATE LIMITING
// ============================================================================
// SECURITY: Rate limiting prevents abuse and DoS attacks.
// This implementation limits by IP address, which has tradeoffs:
//
// Pros: Works for unauthenticated endpoints, simple to implement
// Cons: Users behind NAT/proxy share limits, can be bypassed with IP rotation
//
// PRODUCTION: Also rate limit by authenticated user ID for API endpoints,
// with higher limits for authenticated users.
// ============================================================================
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("api", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 30,
                QueueLimit = 5,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));
});

// === Health Checks ===
builder.Services.AddHealthChecks();

// === Swagger/OpenAPI ===
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Agent Orchestrator API",
        Version = "v1",
        Description = "API for the .NET 10 Agent Orchestrator with M365 Copilot integration"
    });
});

var app = builder.Build();

// === Middleware Pipeline ===
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Agent Orchestrator API v1");
    });
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseSession();
app.UseAuthMiddleware();

app.UseCors();
app.UseRateLimiter();

// === Health Check Endpoints ===
// OPERATIONS: Health check endpoints for container orchestration (Kubernetes, etc.)
// /health - Liveness probe: Is the application running?
// /ready  - Readiness probe: Is the application ready to serve traffic?
// See: https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks
app.MapHealthChecks("/health");
app.MapHealthChecks("/ready");

// === Auth Endpoints (for web channel) ===
app.MapAuthEndpoints();

// === Agent Endpoint (M365 Agents SDK) ===
// Works with Bot Framework Emulator, Azure Bot Service, Teams, and Copilot.
// The channel (emulator/Teams/etc.) provides the serviceUrl for responses.
app.MapPost("/api/messages", async (
    HttpRequest request,
    HttpResponse response,
    IAgentHttpAdapter adapter,
    IAgent agent,
    CancellationToken cancellationToken) =>
{
    await adapter.ProcessAsync(request, response, agent, cancellationToken);
}).RequireRateLimiting("api");

// === Simple Chat Endpoint (for Web UI) ===
// This endpoint provides synchronous responses for the web interface.
// It calls the Semantic Kernel directly without going through the Bot Framework adapter.
app.MapPost("/api/chat", async (
    HttpContext httpContext,
    Kernel kernel,
    ITokenService tokenService,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    try
    {
        var requestBody = await httpContext.Request.ReadFromJsonAsync<ChatRequest>(cancellationToken);
        var message = requestBody?.Message?.Trim();

        if (string.IsNullOrEmpty(message))
        {
            return Results.BadRequest(new { error = "Message is required" });
        }

        logger.LogInformation("Web chat request: {Message}", message);

        // Get access token from session (user is authenticated if they reached here)
        string? accessToken = null;
        var sessionId = httpContext.Session.Id;
        
        logger.LogInformation("Chat request - SessionId: {SessionId}", sessionId);
        
        try
        {
            accessToken = await tokenService.GetAccessTokenAsync(sessionId);
            logger.LogInformation("Access token retrieved successfully");
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "No token found for session {SessionId}", sessionId);
            // Token not found - continue without M365 features
        }

        // Analyze intent
        var intentResult = await kernel.InvokeAsync(
            AgentOrchestrator.Constants.PluginNames.Intent, "AnalyzeIntent",
            new() { ["query"] = message },
            cancellationToken);

        var intentJson = intentResult.GetValue<string>() ?? "[]";
        var intents = System.Text.Json.JsonSerializer.Deserialize<List<AgentOrchestrator.Models.Intent>>(
            intentJson,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new List<AgentOrchestrator.Models.Intent>();

        // Default to general knowledge if no intents detected
        if (intents.Count == 0)
        {
            intents.Add(new AgentOrchestrator.Models.Intent
            {
                Type = AgentOrchestrator.Models.IntentType.GeneralKnowledge,
                Query = message
            });
        }

        // Check if we need M365 auth
        var hasM365Intent = intents.Any(i =>
            i.Type == AgentOrchestrator.Models.IntentType.M365Email ||
            i.Type == AgentOrchestrator.Models.IntentType.M365Calendar ||
            i.Type == AgentOrchestrator.Models.IntentType.M365Files ||
            i.Type == AgentOrchestrator.Models.IntentType.M365People);

        if (hasM365Intent && string.IsNullOrEmpty(accessToken))
        {
            // Filter to general knowledge only
            intents = intents.Where(i => i.Type == AgentOrchestrator.Models.IntentType.GeneralKnowledge).ToList();

            if (intents.Count == 0)
            {
                return Results.Ok(new { text = "Please log in to access your M365 data (emails, calendar, files). Click the Login button above." });
            }
        }

        // Execute based on intent
        var responses = new List<string>();
        foreach (var intent in intents.Take(3)) // Limit to 3 intents
        {
            try
            {
                var result = intent.Type switch
                {
                    AgentOrchestrator.Models.IntentType.GeneralKnowledge =>
                        await kernel.InvokeAsync(AgentOrchestrator.Constants.PluginNames.AzureOpenAI, "GeneralKnowledge",
                            new() { ["query"] = intent.Query }, cancellationToken),
                    AgentOrchestrator.Models.IntentType.M365Email =>
                        await kernel.InvokeAsync(AgentOrchestrator.Constants.PluginNames.M365Copilot, "QueryEmails",
                            new() { ["query"] = intent.Query, ["accessToken"] = accessToken! }, cancellationToken),
                    AgentOrchestrator.Models.IntentType.M365Calendar =>
                        await kernel.InvokeAsync(AgentOrchestrator.Constants.PluginNames.M365Copilot, "QueryCalendar",
                            new() { ["query"] = intent.Query, ["accessToken"] = accessToken! }, cancellationToken),
                    AgentOrchestrator.Models.IntentType.M365Files =>
                        await kernel.InvokeAsync(AgentOrchestrator.Constants.PluginNames.M365Copilot, "QueryFiles",
                            new() { ["query"] = intent.Query, ["accessToken"] = accessToken! }, cancellationToken),
                    AgentOrchestrator.Models.IntentType.M365People =>
                        await kernel.InvokeAsync(AgentOrchestrator.Constants.PluginNames.M365Copilot, "QueryPeople",
                            new() { ["query"] = intent.Query, ["accessToken"] = accessToken! }, cancellationToken),
                    _ => await kernel.InvokeAsync(AgentOrchestrator.Constants.PluginNames.AzureOpenAI, "GeneralKnowledge",
                            new() { ["query"] = intent.Query }, cancellationToken)
                };
                responses.Add(result.GetValue<string>() ?? string.Empty);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error executing intent {Intent}", intent.Type);
                responses.Add($"Error processing {intent.Type}: {ex.Message}");
            }
        }

        // Combine responses
        var finalResponse = responses.Count == 1
            ? responses[0]
            : string.Join("\n\n", responses.Where(r => !string.IsNullOrEmpty(r)));

        if (string.IsNullOrEmpty(finalResponse))
        {
            finalResponse = "I couldn't generate a response. Please try again.";
        }

        return Results.Ok(new { text = finalResponse });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error in chat endpoint");
        // SECURITY: Never expose exception details to clients in production
        // Log full details server-side, return generic message to client
        return Results.Ok(new { text = "Sorry, an error occurred processing your request. Please try again." });
    }
}).RequireRateLimiting("api");

// === Fallback to index.html for SPA ===
app.MapFallbackToFile("index.html");

app.Run();

// Chat request model (must be after app.Run() for top-level statements)
record ChatRequest(string? Message);
