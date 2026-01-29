# .NET 10 Agent Lab Guide

## Introduction

### The Rise of Multi-Agent AI Architectures

Modern AI applications are evolving beyond single-model interactions toward **multi-agent architectures** where specialized agents collaborate to solve complex problems. The **Microsoft 365 Agents SDK** provides an enterprise-grade foundation for building these agents, while **Semantic Kernel** offers powerful orchestration capabilities.

### Microsoft 365 Copilot as a Specialist Agent

**Microsoft 365 Copilot** represents a powerful addition to any agent orchestration strategy. Unlike general-purpose LLMs, M365 Copilot is deeply integrated with your organization's Microsoft 365 ecosystem, providing:

| Capability | Value in Agent Flows |
|------------|---------------------|
| **Enterprise Data Grounding** | Responses are grounded in real emails, calendars, files, and organizational data |
| **Security & Compliance** | Inherits Microsoft 365's security model; respects permissions and data governance |
| **Semantic Understanding** | Understands context across M365 workloads (who sent what, meeting relationships, document history) |
| **Zero Data Preparation** | No need to build RAG pipelines or vector databases for M365 content |

### The Pattern: Agents SDK + Semantic Kernel + Copilot

In this lab, you'll build an orchestration pattern where:

1. **Your .NET 10 Agent** (built on M365 Agents SDK) owns the user experience and orchestration logic
2. **Semantic Kernel Plugins** provide modular AI functions
3. **M365 Copilot** serves as a specialist plugin for enterprise data queries
4. **Azure OpenAI** handles general reasoning, intent classification, and response synthesis

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         YOUR APPLICATION                                │
│                 (M365 Agents SDK + Semantic Kernel)                     │
│                                                                         │
│   "Summarize my project emails and suggest next steps"                  │
│                              │                                          │
│              ┌───────────────┼───────────────┐                          │
│              ▼               ▼               ▼                          │
│   ┌─────────────────┐ ┌─────────────┐ ┌─────────────────┐               │
│   │ M365Copilot     │ │ AzureOpenAI │ │ Synthesis       │               │
│   │ Plugin          │ │ Plugin      │ │ Plugin          │               │
│   └─────────────────┘ └─────────────┘ └─────────────────┘               │
│              │               │               │                          │
│              └───────────────┴───────────────┘                          │
│                              ▼                                          │
│                    Synthesized Response                                 │
└─────────────────────────────────────────────────────────────────────────┘
```

### What You'll Learn

- How to build agents with the Microsoft 365 Agents SDK
- How to use Semantic Kernel for AI orchestration
- How to integrate with M365 Copilot Chat API via Microsoft Graph
- How to synthesize responses from multiple plugins into coherent answers
- How to position your agent for multi-channel deployment

---

## Module 1: Setup & Configuration

### Step 1.1: Clone and Configure

1. Open a terminal and navigate to the lab folder:
   ```bash
   cd chat_api_lab/src/AgentOrchestrator
   ```

2. Open `appsettings.json` and update with your credentials:
   ```json
   {
     "AzureAd": {
       "TenantId": "<your-tenant-id>",
       "ClientId": "<your-client-id>",
       "ClientSecret": "<your-client-secret>"
     },
     "AzureOpenAI": {
       "Endpoint": "https://<your-resource>.openai.azure.com/",
       "ApiKey": "<your-api-key>",
       "DeploymentName": "gpt-4o"
     }
   }
   ```

### Step 1.2: Run the Application

```bash
dotnet run
```

You should see output like:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

### Step 1.3: Test Authentication

1. Open `http://localhost:5000` in your browser
2. Click **Login with Microsoft**
3. Complete the interactive login
4. Verify your username appears in the header

**Checkpoint**: You should see your email/username and the chat input should be enabled.

---

## Module 2: Understanding the Agent Architecture

### Step 2.1: Examine the OrchestratorAgent

Open `Agent/OrchestratorAgent.cs` and review:

1. **Class Declaration** (line 12): Extends `AgentApplication` from M365 Agents SDK
   ```csharp
   public class OrchestratorAgent : AgentApplication
   ```

2. **Activity Handlers** (lines 32-33): Register handlers for message and conversation events
   ```csharp
   OnActivity(ActivityTypes.Message, OnMessageActivityAsync);
   OnActivity(ActivityTypes.ConversationUpdate, OnConversationUpdateAsync);
   ```

3. **Message Processing** (lines 36-93): The main orchestration flow
   - Analyze intent using IntentPlugin
   - Execute plugins in parallel based on intents
   - Synthesize responses using SynthesisPlugin

### Step 2.2: Understand Semantic Kernel Plugins

Open the `Plugins/` folder and examine each plugin:

| File | Purpose |
|------|---------|
| `IntentPlugin.cs` | Classifies user intent into M365 or general knowledge categories |
| `AzureOpenAIPlugin.cs` | Handles general knowledge queries via Azure OpenAI |
| `M365CopilotPlugin.cs` | Calls M365 Copilot Chat API for enterprise data |
| `SynthesisPlugin.cs` | Combines multiple plugin responses into coherent answers |

### Step 2.3: Examine Plugin Structure

Open `Plugins/IntentPlugin.cs`:

```csharp
[KernelFunction]
[Description("Analyzes a user query to identify intent types for routing")]
public async Task<string> AnalyzeIntent(
    [Description("The user's query to analyze")] string query,
    CancellationToken cancellationToken = default)
```

Key elements:
- `[KernelFunction]` - Marks the method as a Semantic Kernel function
- `[Description]` - Provides metadata for the function
- Parameters have descriptions for better LLM understanding

---

## Module 3: Intent Classification

### Step 3.1: Test Intent Classification

Send these messages and observe the console logs:

| Message | Expected Intent(s) |
|---------|-------------------|
| "What's the weather like?" | `GeneralKnowledge` |
| "Summarize my emails" | `M365Email` |
| "What meetings do I have tomorrow?" | `M365Calendar` |
| "Summarize my emails and explain REST APIs" | `M365Email` + `GeneralKnowledge` |

### Step 3.2: Understanding Multi-Intent

The IntentPlugin can detect multiple intents in a single query. This enables:
- Parallel plugin execution
- Comprehensive responses
- Efficient processing

**Try it**: "Find my recent documents and tell me about microservices architecture"

### Step 3.3: Examine the Intent Prompt

Open `Plugins/IntentPlugin.cs` and review the prompt template:

```csharp
var prompt = $$"""
    You are an intent classifier for a multi-agent system...

    Available intent types:
    - M365Email: Questions about emails, messages, inbox, mail
    - M365Calendar: Questions about meetings, schedule, calendar
    - M365Files: Questions about documents, files, SharePoint
    - M365People: Questions about colleagues, organization
    - GeneralKnowledge: General questions not related to M365 data
    ...
    """;
```

---

## Module 4: Microsoft 365 Copilot Chat API Deep Dive

### Understanding the Chat API

The M365 Copilot Chat API allows applications to programmatically interact with Microsoft 365 Copilot on behalf of users. The lab uses a Kiota-generated SDK for type-safe API calls.

**Key Characteristics:**

| Aspect | Detail |
|--------|--------|
| **API Location** | `/beta/copilot/conversations` |
| **SDK** | Kiota-generated CopilotSdk |
| **Auth Model** | Delegated permissions (user context) |
| **Data Access** | Copilot accesses user's M365 data based on their permissions |
| **Response Type** | Natural language, grounded in enterprise data |
| **Licensing** | Requires Microsoft 365 Copilot license per user |

### Step 4.1: Examine M365CopilotPlugin

Open `Plugins/M365CopilotPlugin.cs` and review:

1. **Plugin Functions** (QueryEmails, QueryCalendar, QueryFiles, QueryPeople):
   ```csharp
   [KernelFunction]
   [Description("Query M365 Copilot for email-related questions")]
   public async Task<string> QueryEmails(
       [Description("The email-related question")] string query,
       [Description("User's access token for Graph API")] string accessToken,
       CancellationToken cancellationToken = default)
   ```

2. **Chat API Flow** (using Kiota SDK):
   - Create conversation: `client.Copilot.Conversations.PostAsync()`
   - Send chat and get response: `client.Copilot.Conversations[id].Chat.PostAsync()`

### Step 4.2: Test M365 Queries

Try these M365-specific queries:

| Domain | Example Query | What Copilot Accesses |
|--------|---------------|----------------------|
| **Email** | "What important emails did I receive today?" | Exchange mailbox |
| **Calendar** | "Do I have any meetings this afternoon?" | Outlook calendar |
| **Files** | "What documents did I edit this week?" | OneDrive, SharePoint |
| **People** | "Who works on the marketing team?" | Azure AD, Org data |

### Step 4.3: Chat API Considerations

| Aspect | Detail |
|--------|--------|
| **Beta API** | Endpoints are in `/beta`, subject to change |
| **Synchronous** | Response returned directly (no polling needed) |
| **Rate Limits** | Subject to Graph API rate limiting |
| **License Required** | Each user needs M365 Copilot license |
| **SDK** | Kiota-generated client provides type safety |

---

## Module 5: Response Synthesis

### Step 5.1: Understanding Synthesis

When multiple plugins return data, the SynthesisPlugin combines them into a coherent response.

Open `Plugins/SynthesisPlugin.cs`:

```csharp
[KernelFunction]
[Description("Synthesizes multiple agent responses into a coherent unified response")]
public async Task<string> Synthesize(
    [Description("The original user query")] string originalQuery,
    [Description("JSON array of agent responses")] string responses,
    CancellationToken cancellationToken = default)
```

### Step 5.2: Test Synthesis

Send a multi-intent query and observe how responses are combined:

```
"Summarize the emails I received from my manager this week and explain what a REST API is"
```

**Observe**:
1. Intent analysis detects two intents
2. Both plugins execute (potentially in parallel)
3. Synthesis step combines the results coherently

---

## Module 6: Multi-Channel Architecture

### Step 6.1: Understanding the Activity Protocol

The M365 Agents SDK uses the Activity Protocol (Bot Framework compatible) for communication:

```json
{
  "type": "message",
  "text": "User's message",
  "from": { "id": "user-id", "name": "User Name" },
  "conversation": { "id": "conversation-id" },
  "channelId": "webchat"
}
```

### Step 6.2: Channel Support

Your agent can deploy to multiple channels without code changes:

| Channel | How to Enable |
|---------|---------------|
| **Web** | ✓ Implemented via `/api/messages` |
| **Teams** | Add Teams app manifest + Azure Bot registration |
| **M365 Copilot** | Configure as Copilot plugin |
| **Slack** | Add Slack adapter configuration |

### Step 6.3: Examine Program.cs

Open `Program.cs` and review the agent registration:

```csharp
// Add AgentApplicationOptions from configuration
builder.AddAgentApplicationOptions();

// Register the agent
builder.AddAgent<OrchestratorAgent>();

// Agent endpoint
app.MapPost("/api/messages", async (HttpRequest request, HttpResponse response,
    IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
{
    await adapter.ProcessAsync(request, response, agent, cancellationToken);
});
```

---

## Module 7: Extending the System (Optional)

### Challenge 1: Add a New Plugin

Create a new Semantic Kernel plugin for a custom data source:

1. Create `Plugins/CustomPlugin.cs`:
   ```csharp
   public class CustomPlugin
   {
       [KernelFunction]
       [Description("Your custom function description")]
       public async Task<string> CustomFunction(
           [Description("Parameter description")] string input,
           CancellationToken cancellationToken = default)
       {
           // Your implementation
       }
   }
   ```

2. Register in `Program.cs`:
   ```csharp
   kernel.Plugins.AddFromObject(new CustomPlugin(), "CustomPlugin");
   ```

3. Update `IntentPlugin.cs` to recognize the new intent type

### Challenge 2: Add Teams Channel

1. Create Azure Bot registration
2. Add Teams app manifest
3. Configure bot endpoint
4. Deploy and test in Teams

### Challenge 3: Add Conversation Memory

Implement turn state to maintain conversation history:

```csharp
// In OrchestratorAgent
ChatHistory chatHistory = turnState.GetValue(
    "conversation.chatHistory",
    () => new ChatHistory()
);
```

---

## Summary

In this lab, you learned how to:

1. **Build with M365 Agents SDK** - Enterprise-grade agent framework
2. **Use Semantic Kernel** - Plugin-based AI orchestration
3. **Integrate M365 Copilot** - Enterprise data grounding via Chat API
4. **Handle Multi-Intent** - Parallel plugin execution
5. **Synthesize Responses** - Combine outputs into coherent answers

### Key Takeaways

- The M365 Agents SDK provides production-ready agent infrastructure
- Semantic Kernel plugins enable modular AI function design
- M365 Copilot Chat API provides enterprise data without RAG pipelines
- The Activity Protocol enables multi-channel deployment
- Parallel execution improves response times for multi-intent queries

### Next Steps

- Deploy to Microsoft Teams
- Configure as M365 Copilot plugin
- Add conversation memory
- Implement additional plugins
- Add telemetry with Application Insights

---

## Appendix: Code Reference

| File | Purpose |
|------|---------|
| `Program.cs` | Application entry, DI, agent registration |
| `Agent/OrchestratorAgent.cs` | Main agent (AgentApplication) |
| `Plugins/IntentPlugin.cs` | Intent classification |
| `Plugins/AzureOpenAIPlugin.cs` | General knowledge queries |
| `Plugins/M365CopilotPlugin.cs` | M365 Copilot Chat API integration |
| `Plugins/SynthesisPlugin.cs` | Response combination |
| `CopilotSdk/` | Kiota-generated Copilot API client |
| `Auth/TokenService.cs` | MSAL token management |
| `Auth/AuthEndpoints.cs` | Login/logout routes |
