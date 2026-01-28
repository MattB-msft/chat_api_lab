# .NET 10 Agent on Microsoft 365 Agents SDK

> **Note**: This is a migration reference document. For current architecture details, see [DESIGN.md](../../DESIGN.md).

## Overview

This document outlines the design for migrating the .NET 10 Agent from a custom Minimal API implementation to the **Microsoft 365 Agents SDK**. This migration enables multi-channel deployment (Teams, M365 Copilot, Web) while maintaining the agent-to-agent orchestration pattern with M365 Copilot Chat API.

## Why Microsoft 365 Agents SDK?

| Benefit | Description |
|---------|-------------|
| **Multi-Channel Deployment** | Deploy once, run on Teams, M365 Copilot, Web, Slack, and 10+ channels |
| **Enterprise-Grade Infrastructure** | Built-in state management, storage, and conversation handling |
| **Semantic Kernel Integration** | First-class support for SK orchestration and plugins |
| **Evolution of Bot Framework** | Production-ready, GA status, actively maintained |
| **M365 Copilot Extension** | Can surface as a Copilot extension in M365 |

## Architecture Comparison

### Current Architecture (Custom Minimal API)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              WEB UI (Custom)                                │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         .NET 10 MINIMAL API                                 │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  OrchestratorAgent → IntentAnalyzer → PlanExecutor → Synthesizer    │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────────────┘
           │                                              │
           ▼                                              ▼
    ┌─────────────┐                            ┌─────────────────────┐
    │ Azure OpenAI│                            │ M365 Copilot Chat   │
    └─────────────┘                            │ API (Graph)         │
                                               └─────────────────────┘
```

### Proposed Architecture (M365 Agents SDK + Semantic Kernel)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           MULTI-CHANNEL CLIENTS                             │
│   ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐      │
│   │  Teams   │  │ M365     │  │   Web    │  │  Slack   │  │ Custom   │      │
│   │          │  │ Copilot  │  │  Chat    │  │          │  │  Client  │      │
│   └──────────┘  └──────────┘  └──────────┘  └──────────┘  └──────────┘      │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                          ┌───────────┴───────────┐
                          │    Activity Protocol   │
                          └───────────┬───────────┘
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                      MICROSOFT 365 AGENTS SDK                               │
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                         CloudAdapter                                │    │
│  │              (HTTP ↔ Activity Protocol Translation)                 │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                      │                                      │
│                                      ▼                                      │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                    OrchestratorAgent : AgentApplication             │    │
│  │                                                                     │    │
│  │   OnActivity(Message) ──► Semantic Kernel ──► Plugins/Functions     │    │
│  │                                                                     │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                      │                                      │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                        SEMANTIC KERNEL                              │    │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐                  │    │
│  │  │ Intent      │  │ Plan        │  │ Response    │                  │    │
│  │  │ Plugin      │  │ Plugin      │  │ Synthesizer │                  │    │
│  │  └─────────────┘  └─────────────┘  └─────────────┘                  │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                      │                                      │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                        SPECIALIST PLUGINS                           │    │
│  │  ┌───────────────────────┐      ┌───────────────────────────────┐   │    │
│  │  │ AzureOpenAIPlugin     │      │ M365CopilotPlugin             │   │    │
│  │  │ • GeneralKnowledge()  │      │ • QueryEmails()               │   │    │
│  │  │ • AnalyzeIntent()     │      │ • QueryCalendar()             │   │    │
│  │  │ • SynthesizeResponse()│      │ • QueryFiles()                │   │    │
│  │  └───────────────────────┘      │ • QueryPeople()               │   │    │
│  │                                 └───────────────────────────────┘   │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────────────┘
           │                                              │
           ▼                                              ▼
    ┌─────────────┐                            ┌─────────────────────┐
    │ Azure OpenAI│                            │ M365 Copilot Chat   │
    │ (GPT-4o)    │                            │ API (Graph /beta)   │
    └─────────────┘                            └─────────────────────┘
```

---

## Project Structure

```
src/AgentOrchestrator/
├── AgentOrchestrator.csproj
├── Program.cs                          # Host setup, DI, routing
├── appsettings.json
│
├── Agent/
│   └── OrchestratorAgent.cs            # AgentApplication implementation
│
├── Plugins/                            # Semantic Kernel Plugins
│   ├── IntentPlugin.cs                 # Intent classification
│   ├── AzureOpenAIPlugin.cs            # General knowledge queries
│   ├── M365CopilotPlugin.cs            # M365 Copilot Chat API calls
│   └── SynthesisPlugin.cs              # Response synthesis
│
├── CopilotSdk/                         # Kiota-generated API client
│   ├── CopilotApi.cs                   # Main entry point
│   ├── Copilot/                        # Request builders
│   └── Models/                         # Request/response models
│
├── Services/
│   ├── TokenService.cs                 # MSAL token acquisition
│   └── GraphClientFactory.cs           # Microsoft Graph client
│
├── Models/
│   ├── Intent.cs
│   ├── ExecutionPlan.cs
│   └── AgentResponse.cs
│
└── wwwroot/                            # Web channel UI (optional)
    ├── index.html
    └── js/
```

---

## Key Components

### 1. Program.cs - Host Configuration

```csharp
using Microsoft.Agents.Builder;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.SemanticKernel;

var builder = WebApplication.CreateBuilder(args);

// === Semantic Kernel Setup ===
builder.Services.AddKernel()
    .AddAzureOpenAIChatCompletion(
        deploymentName: builder.Configuration["AzureOpenAI:DeploymentName"]!,
        endpoint: builder.Configuration["AzureOpenAI:Endpoint"]!,
        apiKey: builder.Configuration["AzureOpenAI:ApiKey"]!
    );

// === Register Plugins ===
builder.Services.AddSingleton<IntentPlugin>();
builder.Services.AddSingleton<AzureOpenAIPlugin>();
builder.Services.AddSingleton<M365CopilotPlugin>();
builder.Services.AddSingleton<SynthesisPlugin>();

builder.Services.AddKernel()
    .Plugins.AddFromType<IntentPlugin>()
    .Plugins.AddFromType<AzureOpenAIPlugin>()
    .Plugins.AddFromType<M365CopilotPlugin>()
    .Plugins.AddFromType<SynthesisPlugin>();

// === M365 Agents SDK Setup ===
builder.Services.AddSingleton<IStorage, MemoryStorage>();
builder.Services.AddSingleton<CloudAdapter>();
builder.Services.AddSingleton<AgentApplicationOptions>(sp =>
    new AgentApplicationOptions
    {
        StartTypingTimer = true,
        LongRunningMessages = true
    });

// === Register Agent ===
builder.AddAgent<OrchestratorAgent>();

// === Auth Services ===
builder.Services.AddSingleton<TokenService>();

var app = builder.Build();

// === Routing ===
app.MapPost("/api/messages", async (HttpContext context, CloudAdapter adapter, OrchestratorAgent agent) =>
{
    await adapter.ProcessAsync(context.Request, context.Response, agent);
});

app.MapStaticAssets();
app.MapFallbackToFile("index.html");

app.Run();
```

### 2. OrchestratorAgent.cs - Agent Implementation

```csharp
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Protocols.Primitives;
using Microsoft.SemanticKernel;

public class OrchestratorAgent : AgentApplication
{
    private readonly Kernel _kernel;
    private readonly ILogger<OrchestratorAgent> _logger;

    public OrchestratorAgent(
        AgentApplicationOptions options,
        Kernel kernel,
        ILogger<OrchestratorAgent> logger) : base(options)
    {
        _kernel = kernel;
        _logger = logger;

        // Register activity handlers
        OnActivity(ActivityTypes.Message, OnMessageActivityAsync);
        OnActivity(ActivityTypes.ConversationUpdate, OnConversationUpdateAsync);
    }

    private async Task OnMessageActivityAsync(
        ITurnContext turnContext,
        ITurnState turnState,
        CancellationToken cancellationToken)
    {
        var userMessage = turnContext.Activity.Text?.Trim() ?? string.Empty;

        _logger.LogInformation("Received message: {Message}", userMessage);

        // Step 1: Analyze Intent using Semantic Kernel
        var intentResult = await _kernel.InvokeAsync(
            "IntentPlugin",
            "AnalyzeIntent",
            new() { ["query"] = userMessage },
            cancellationToken);

        var intents = intentResult.GetValue<List<Intent>>() ?? [];

        // Step 2: Execute plan based on intents (parallel when possible)
        var tasks = new List<Task<AgentResponse>>();

        foreach (var intent in intents)
        {
            var task = intent.Type switch
            {
                IntentType.M365Email or
                IntentType.M365Calendar or
                IntentType.M365Files or
                IntentType.M365People => ExecuteM365PluginAsync(intent, turnContext, cancellationToken),

                IntentType.GeneralKnowledge => ExecuteGeneralKnowledgeAsync(intent, cancellationToken),

                _ => Task.FromResult(new AgentResponse { Content = "Unknown intent" })
            };
            tasks.Add(task);
        }

        var responses = await Task.WhenAll(tasks);

        // Step 3: Synthesize responses
        var synthesisResult = await _kernel.InvokeAsync(
            "SynthesisPlugin",
            "Synthesize",
            new()
            {
                ["originalQuery"] = userMessage,
                ["responses"] = responses
            },
            cancellationToken);

        var finalResponse = synthesisResult.GetValue<string>() ?? "I couldn't process your request.";

        // Step 4: Send response
        await turnContext.SendActivityAsync(
            MessageFactory.Text(finalResponse),
            cancellationToken);
    }

    private async Task<AgentResponse> ExecuteM365PluginAsync(
        Intent intent,
        ITurnContext turnContext,
        CancellationToken cancellationToken)
    {
        // Get user's access token from turn context (passed via SSO or token exchange)
        var accessToken = await GetUserAccessTokenAsync(turnContext, cancellationToken);

        var result = await _kernel.InvokeAsync(
            "M365CopilotPlugin",
            intent.Type.ToString(),
            new()
            {
                ["query"] = intent.Query,
                ["accessToken"] = accessToken
            },
            cancellationToken);

        return new AgentResponse
        {
            Agent = "m365_copilot",
            IntentType = intent.Type,
            Content = result.GetValue<string>() ?? string.Empty
        };
    }

    private async Task<AgentResponse> ExecuteGeneralKnowledgeAsync(
        Intent intent,
        CancellationToken cancellationToken)
    {
        var result = await _kernel.InvokeAsync(
            "AzureOpenAIPlugin",
            "GeneralKnowledge",
            new() { ["query"] = intent.Query },
            cancellationToken);

        return new AgentResponse
        {
            Agent = "azure_openai",
            IntentType = intent.Type,
            Content = result.GetValue<string>() ?? string.Empty
        };
    }

    private async Task OnConversationUpdateAsync(
        ITurnContext turnContext,
        ITurnState turnState,
        CancellationToken cancellationToken)
    {
        // Welcome message when user joins
        if (turnContext.Activity.MembersAdded?.Any(m => m.Id != turnContext.Activity.Recipient.Id) == true)
        {
            await turnContext.SendActivityAsync(
                MessageFactory.Text("Welcome! I can help you with M365 data and general questions."),
                cancellationToken);
        }
    }
}
```

### 3. M365CopilotPlugin.cs - Copilot Chat API Integration

The plugin uses a Kiota-generated SDK for type-safe API calls:

```csharp
using Microsoft.SemanticKernel;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using AgentOrchestrator.CopilotSdk;
using CopilotChatRequest = AgentOrchestrator.CopilotSdk.Models.ChatRequest;
using CopilotMessageParameter = AgentOrchestrator.CopilotSdk.Models.MessageParameter;
using CopilotLocationHint = AgentOrchestrator.CopilotSdk.Models.LocationHint;

public class M365CopilotPlugin
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<M365CopilotPlugin> _logger;

    public M365CopilotPlugin(
        IHttpClientFactory httpClientFactory,
        ILogger<M365CopilotPlugin> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [KernelFunction]
    [Description("Query M365 Copilot for email-related questions")]
    public async Task<string> QueryEmails(
        [Description("The email-related question")] string query,
        [Description("User's access token for Graph API")] string accessToken,
        CancellationToken cancellationToken = default)
    {
        return await CallCopilotChatApiAsync(query, accessToken, cancellationToken);
    }

    // Similar functions for QueryCalendar, QueryFiles, QueryPeople...

    private async Task<string> CallCopilotChatApiAsync(
        string query,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var client = CreateCopilotClient(accessToken);

        // Step 1: Create conversation
        var conversation = await client.Copilot.Conversations.PostAsync(
            new AgentOrchestrator.CopilotSdk.Copilot.Conversations.ConversationsPostRequestBody(),
            cancellationToken: cancellationToken);

        // Step 2: Send chat message and get response
        var chatRequest = new CopilotChatRequest
        {
            Message = new CopilotMessageParameter { Text = query },
            LocationHint = new CopilotLocationHint { TimeZone = "UTC" }
        };

        var response = await client.Copilot.Conversations[conversation.Id.Value]
            .Chat.PostAsync(chatRequest, cancellationToken: cancellationToken);

        return response?.Messages?.LastOrDefault()?.Text ?? "No response content.";
    }

    private CopilotApi CreateCopilotClient(string accessToken)
    {
        var httpClient = _httpClientFactory.CreateClient("Graph");
        var authProvider = new BaseBearerTokenAuthenticationProvider(
            new TokenProvider(accessToken));
        var adapter = new HttpClientRequestAdapter(authProvider, httpClient: httpClient);
        return new CopilotApi(adapter);
    }
}
```

### 4. IntentPlugin.cs - Intent Classification

```csharp
using Microsoft.SemanticKernel;
using System.ComponentModel;

public class IntentPlugin
{
    private readonly Kernel _kernel;

    public IntentPlugin(Kernel kernel)
    {
        _kernel = kernel;
    }

    [KernelFunction]
    [Description("Analyzes user query to determine intent types for routing")]
    public async Task<List<Intent>> AnalyzeIntent(
        [Description("The user's query to analyze")] string query,
        CancellationToken cancellationToken = default)
    {
        var prompt = $"""
            Analyze the following user query and identify the intent types.

            Intent types:
            - M365Email: Questions about emails, messages, inbox
            - M365Calendar: Questions about meetings, schedule, calendar
            - M365Files: Questions about documents, files, SharePoint
            - M365People: Questions about colleagues, org structure, expertise
            - GeneralKnowledge: General questions not related to M365 data

            Query: {query}

            Return a JSON array of intents:
            [{{ "type": "IntentType", "query": "extracted sub-query" }}]

            If multiple intents exist, return all of them.
            """;

        var result = await _kernel.InvokePromptAsync(prompt, cancellationToken: cancellationToken);

        // Parse JSON response into Intent objects
        var json = result.GetValue<string>() ?? "[]";
        return JsonSerializer.Deserialize<List<Intent>>(json) ?? [];
    }
}
```

---

## Channel Configuration

### Teams Channel

Register the agent as a Teams app:

```json
// manifest.json
{
  "$schema": "https://developer.microsoft.com/json-schemas/teams/v1.17/MicrosoftTeams.schema.json",
  "version": "1.0.0",
  "id": "{app-id}",
  "name": {
    "short": "M365 Orchestrator"
  },
  "bots": [
    {
      "botId": "{bot-id}",
      "scopes": ["personal", "team", "groupChat"],
      "supportsFiles": false
    }
  ]
}
```

### M365 Copilot Extension

Configure as a Copilot plugin for declarative agent scenarios:

```json
// copilot-plugin.json
{
  "schema_version": "v2.1",
  "name_for_human": "M365 Orchestrator",
  "description_for_human": "Query your M365 data with AI assistance",
  "api": {
    "type": "openapi",
    "url": "https://{your-domain}/openapi.json"
  }
}
```

### Web Channel

The existing web UI continues to work via the `/api/messages` endpoint.

---

## Authentication Flow

### Teams SSO

```csharp
// In OrchestratorAgent.cs
private async Task<string> GetUserAccessTokenAsync(
    ITurnContext turnContext,
    CancellationToken cancellationToken)
{
    // For Teams, use SSO token exchange
    if (turnContext.Activity.ChannelId == Channels.MsTeams)
    {
        var tokenResponse = await turnContext.Adapter.GetUserTokenAsync(
            turnContext,
            _config["AzureAd:ConnectionName"],
            null,
            cancellationToken);

        return tokenResponse?.Token ?? throw new UnauthorizedAccessException();
    }

    // For web channel, token passed in activity
    return turnContext.Activity.Value?.ToString() ?? throw new UnauthorizedAccessException();
}
```

---

## NuGet Packages

```xml
<ItemGroup>
    <!-- M365 Agents SDK -->
    <PackageReference Include="Microsoft.Agents.Builder" Version="1.*" />
    <PackageReference Include="Microsoft.Agents.Hosting.AspNetCore" Version="1.*" />

    <!-- Kiota (for Copilot SDK) -->
    <PackageReference Include="Microsoft.Kiota.Abstractions" Version="1.*" />
    <PackageReference Include="Microsoft.Kiota.Http.HttpClientLibrary" Version="1.*" />
    <PackageReference Include="Microsoft.Kiota.Serialization.Json" Version="1.*" />
    <PackageReference Include="Microsoft.Kiota.Serialization.Text" Version="1.*" />
    <PackageReference Include="Microsoft.Kiota.Serialization.Form" Version="1.*" />
    <PackageReference Include="Microsoft.Kiota.Serialization.Multipart" Version="1.*" />

    <!-- Semantic Kernel -->
    <PackageReference Include="Microsoft.SemanticKernel" Version="1.*" />
    <PackageReference Include="Microsoft.SemanticKernel.Connectors.AzureOpenAI" Version="1.*" />

    <!-- Authentication -->
    <PackageReference Include="Microsoft.Identity.Web" Version="3.*" />
</ItemGroup>
```

---

## Migration Path

| Current Component | M365 Agents SDK Equivalent |
|-------------------|---------------------------|
| `Program.cs` (Minimal API) | `Program.cs` (AddAgent, CloudAdapter) |
| `OrchestratorAgent.cs` | `OrchestratorAgent : AgentApplication` |
| `IntentAnalyzer.cs` | `IntentPlugin.cs` (Kernel Function) |
| `PlanBuilder.cs` + `PlanExecutor.cs` | Built into `OrchestratorAgent.OnMessageActivityAsync` |
| `AzureOpenAIAgent.cs` | `AzureOpenAIPlugin.cs` (Kernel Function) |
| `M365CopilotAgent.cs` | `M365CopilotPlugin.cs` (Kernel Function) |
| `ResponseSynthesizer.cs` | `SynthesisPlugin.cs` (Kernel Function) |
| `StreamingService.cs` | SDK handles via Activity Protocol |
| Custom SSE endpoints | Not needed - SDK manages channels |

---

## Benefits of Migration

1. **Multi-Channel Ready**: Same agent code works on Teams, Copilot, Web, Slack
2. **Semantic Kernel Native**: Clean plugin architecture for AI functions
3. **Production Infrastructure**: Built-in state, storage, and conversation management
4. **Teams SSO**: Native single sign-on support
5. **M365 Copilot Integration**: Can extend M365 Copilot directly
6. **Future-Proof**: Active development, replacing deprecated Bot Framework

---

## References

- [Microsoft 365 Agents SDK Documentation](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/)
- [Agents SDK GitHub (.NET)](https://github.com/microsoft/Agents-for-net)
- [Semantic Kernel Integration Guide](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/using-semantic-kernel-agent-framework)
- [Creating Agents in Visual Studio](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/create-new-toolkit-project-vs)
- [Semantic Kernel Multi-turn Sample](https://github.com/microsoft/Agents/tree/main/samples/dotnet)

---

*Design Version: 1.0*
*Last Updated: January 2026*
