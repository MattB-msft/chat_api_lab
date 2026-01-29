using AgentOrchestrator;
using AgentOrchestrator.Agent;
using AgentOrchestrator.Models;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.SemanticKernel;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// CONFIGURATION
// ============================================================================
// SECURITY: Secrets (ClientSecret, ApiKey) should be loaded from:
// - Development: dotnet user-secrets (see appsettings.Development.json.template)
// - Production: Azure Key Vault or environment variables
// Never commit secrets to version control!
// ============================================================================

//// === Load Configuration ===
var azureOpenAISettings = builder.Configuration.GetSection("AIServices:AzureOpenAI").Get<AzureOpenAISettings>()
    ?? throw new InvalidOperationException("AzureOpenAI configuration is required");

var graphSettings = builder.Configuration.GetSection("MicrosoftGraph").Get<MicrosoftGraphSettings>()
    ?? throw new InvalidOperationException("MicrosoftGraph configuration is required");

var orchestrationSettings = builder.Configuration.GetSection("Orchestration").Get<OrchestrationSettings>()
    ?? new OrchestrationSettings();

// Register configuration as singletons
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

    // SECURITY: HttpOnly prevents JavaScript access to session cookie
    // This mitigates XSS attacks that try to steal session tokens
    options.Cookie.HttpOnly = true;

    options.Cookie.IsEssential = true;

    // SECURITY: SameSite prevents CSRF by not sending cookie on cross-site requests
    // Lax: Cookie sent on top-level navigation (GET) but not on cross-site POST
    // Strict: Cookie never sent on cross-site requests (more secure but may break OAuth)
    options.Cookie.SameSite = SameSiteMode.Lax;

    // PRODUCTION: Add these for HTTPS deployments
    // options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

builder.Services.AddHttpContextAccessor();

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
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(640);

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

    return kernel;
});

// Add AspNet token validation
builder.Services.AddAgentAspNetAuthentication(builder.Configuration);

// === M365 Agents SDK Setup ===
// LAB SIMPLIFICATION: MemoryStorage stores conversation state in-memory.
// State is lost on app restart. For production, implement IStorage with
// Azure Cosmos DB, SQL Server, or Azure Blob Storage.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

// Add AgentApplicationOptions from configuration
builder.AddAgentApplicationOptions();

// Register the agent
builder.AddAgent<OrchestratorAgent>();

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
        Description = "API for the Agent Orchestrator with M365 Copilot integration"
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
app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

// === Health Check Endpoints ===
// OPERATIONS: Health check endpoints for container orchestration (Kubernetes, etc.)
// /health - Liveness probe: Is the application running?
// /ready  - Readiness probe: Is the application ready to serve traffic?
// See: https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks
app.MapHealthChecks("/health");
app.MapHealthChecks("/ready");

// === Agent Endpoint (M365 Agents SDK) ===
app.MapPost("/api/messages", async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
{
    await adapter.ProcessAsync(request, response, agent, cancellationToken);
}).RequireRateLimiting("api");

if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "Playground" || app.Environment.EnvironmentName == "local")
{
    app.UseDeveloperExceptionPage();

    // Hard coded for brevity and ease of testing. 
    // In production, this should be set in configuration.
    app.Urls.Add($"http://localhost:3978");
}

app.Run();
