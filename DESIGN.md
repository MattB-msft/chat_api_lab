# Agent-to-Agent Communication Lab: .NET 10 Agent + M365 Copilot

## Overview

### The Value of M365 Copilot in Agent Orchestrations

Multi-agent AI architectures benefit from specialist agents that bring unique capabilities. **Microsoft 365 Copilot** is an ideal specialist for enterprise scenarios because it provides:

| Capability | Benefit |
|------------|---------|
| **Enterprise Data Grounding** | Responses based on actual emails, calendar, files—not hallucinations |
| **Security & Compliance** | Inherits M365 permissions; no custom access control needed |
| **Semantic Understanding** | Knows M365 relationships (people, meetings, document history) |
| **Zero Infrastructure** | No RAG pipelines, vector DBs, or embeddings to maintain |

### What This Lab Demonstrates

This lab shows how to build a **.NET 10 Agent** using the **Microsoft 365 Agents SDK** with **Semantic Kernel** orchestration. The agent analyzes user intent, routes M365-related queries to Copilot Chat API, and synthesizes responses from multiple sources into a unified answer.

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
                          │    Activity Protocol  │
                          └───────────┬───────────┘
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                      MICROSOFT 365 AGENTS SDK                               │
│                                                                             │
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
│  │  │ Intent      │  │ AzureOpenAI │  │ Synthesis   │                  │    │
│  │  │ Plugin      │  │ Plugin      │  │ Plugin      │                  │    │
│  │  └─────────────┘  └─────────────┘  └─────────────┘                  │    │
│  │                         │                                           │    │
│  │                  ┌──────┴──────┐                                    │    │
│  │                  │M365Copilot  │                                    │    │
│  │                  │Plugin       │                                    │    │
│  │                  └─────────────┘                                    │    │
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

## Technology Stack

| Layer | Technology |
|-------|------------|
| **Agent Framework** | Microsoft 365 Agents SDK 1.1.x |
| **Orchestration** | Semantic Kernel 1.54.x |
| **AI Model** | Azure OpenAI (GPT-4o) |
| **M365 Integration** | Copilot Chat API via Kiota SDK |
| **Authentication** | MSAL, Microsoft Entra ID |
| **Runtime** | .NET 10, ASP.NET Core |
| **Frontend** | Vanilla JavaScript |

---

## Architecture Components

### 1. OrchestratorAgent (AgentApplication)

The main agent class extends `AgentApplication` from the M365 Agents SDK:

**Responsibilities:**
- Receives user messages via Activity Protocol
- Analyzes intent using Semantic Kernel
- Routes to appropriate plugins (M365 Copilot or Azure OpenAI)
- Synthesizes multi-agent responses
- Sends responses back to the client

**Key Methods:**
- `OnMessageActivityAsync` - Handles incoming messages
- `OnConversationUpdateAsync` - Handles welcome messages
- `AnalyzeIntentAsync` - Invokes IntentPlugin
- `ExecuteAgentsAsync` - Parallel plugin execution
- `SynthesizeResponseAsync` - Combines multiple responses

### 2. Semantic Kernel Plugins

AI functions are organized as Semantic Kernel plugins:

| Plugin | Purpose | Functions |
|--------|---------|-----------|
| **IntentPlugin** | Classify user intent | `AnalyzeIntent` |
| **AzureOpenAIPlugin** | General knowledge | `GeneralKnowledge` |
| **M365CopilotPlugin** | M365 data queries | `QueryEmails`, `QueryCalendar`, `QueryFiles`, `QueryPeople` |
| **SynthesisPlugin** | Combine responses | `Synthesize` |

### 3. M365 Copilot Chat API Integration

The M365CopilotPlugin uses a Kiota-generated SDK to call the Copilot Chat API:

```
┌─────────────────────────────────────────────────────────────────────┐
│  STEP 1: Create Conversation                                         │
│  POST /beta/copilot/conversations                                    │
│  → Returns: conversationId                                          │
└─────────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────────┐
│  STEP 2: Send Chat Message                                           │
│  POST /beta/copilot/conversations/{conversationId}/chat              │
│  Body: { "message": { "text": "Your question" }, "locationHint": {} }│
│  → Returns: Copilot's response immediately                          │
└─────────────────────────────────────────────────────────────────────┘
```

The SDK provides strongly-typed models and handles serialization automatically.

---

## Authentication Flow

```
┌──────────┐     ┌──────────────┐     ┌─────────────────┐     ┌──────────────┐
│  User    │────▶│  Web UI      │────▶│  Backend API    │────▶│  Microsoft   │
│          │     │              │     │                 │     │  Entra ID    │
└──────────┘     └──────────────┘     └─────────────────┘     └──────────────┘
                        │                      │                      │
                        │  1. Click Login      │                      │
                        │─────────────────────▶│                      │
                        │                      │  2. Redirect to      │
                        │                      │     /authorize       │
                        │                      │─────────────────────▶│
                        │                      │                      │
                        │  3. Interactive login (browser)             │
                        │◀────────────────────────────────────────────│
                        │                      │                      │
                        │  4. Auth code        │                      │
                        │─────────────────────▶│  5. Exchange for     │
                        │                      │     tokens           │
                        │                      │─────────────────────▶│
                        │                      │                      │
                        │                      │  6. Access + Refresh │
                        │                      │◀─────────────────────│
                        │  7. Session established                     │
                        │◀─────────────────────│                      │
```

**Required Scopes:**
```
openid, profile, email, User.Read
Mail.Read, Calendars.Read, Files.Read.All
Sites.Read.All, People.Read.All, Chat.Read
OnlineMeetingTranscript.Read.All, ChannelMessage.Read.All, ExternalItem.Read.All
```

---

## Orchestration Flow

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ QUERY: "Summarize my emails from today and tell me about quantum computing" │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│ STEP 1: INTENT ANALYSIS (IntentPlugin)                                      │
│                                                                             │
│ Input: User query                                                           │
│ Output: [                                                                   │
│   { "type": "M365Email", "query": "summarize emails from today" },          │
│   { "type": "GeneralKnowledge", "query": "explain quantum computing" }      │
│ ]                                                                           │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                          ┌───────────┴───────────┐
                          ▼                       ▼
┌─────────────────────────────────┐  ┌─────────────────────────────────┐
│ STEP 2A: M365CopilotPlugin      │  │ STEP 2B: AzureOpenAIPlugin      │
│                                 │  │                                 │
│ QueryEmails("summarize...")     │  │ GeneralKnowledge("explain...")  │
│ → Calls Copilot Chat API        │  │ → Calls Azure OpenAI            │
│ → Returns email summary         │  │ → Returns explanation           │
└─────────────────────────────────┘  └─────────────────────────────────┘
                          │                       │
                          └───────────┬───────────┘
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│ STEP 3: RESPONSE SYNTHESIS (SynthesisPlugin)                                │
│                                                                             │
│ Combine responses into coherent, unified answer                             │
│ Output: Final synthesized response to user                                  │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Project Structure

```
src/AgentOrchestrator/
├── AgentOrchestrator.csproj
├── Program.cs                      # App entry, DI, agent registration
├── appsettings.json                # Configuration
│
├── Agent/
│   └── OrchestratorAgent.cs        # Main agent (AgentApplication)
│
├── Plugins/                        # Semantic Kernel Plugins
│   ├── IntentPlugin.cs             # Intent classification
│   ├── AzureOpenAIPlugin.cs        # General knowledge queries
│   ├── M365CopilotPlugin.cs        # Copilot Chat API integration
│   └── SynthesisPlugin.cs          # Response combination
│
├── CopilotSdk/                     # Kiota-generated Copilot API client
│   ├── CopilotApi.cs               # Main API client
│   ├── Copilot/                    # Conversations and chat builders
│   └── Models/                     # Request/response models
│
├── Auth/
│   ├── AuthEndpoints.cs            # Login/logout/callback routes
│   ├── TokenService.cs             # MSAL token management
│   └── AuthMiddleware.cs           # Session validation
│
├── Models/
│   ├── Intent.cs                   # Intent types and models
│   ├── AgentResponse.cs            # Response model
│   └── Configuration.cs            # Configuration models
│
└── wwwroot/                        # Web UI
    ├── index.html
    ├── css/styles.css
    └── js/
        ├── app.js
        ├── auth.js
        ├── chat.js
        └── trace.js
```

---

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/auth/login` | Initiates OAuth2 login flow |
| GET | `/auth/callback` | OAuth2 callback handler |
| POST | `/auth/logout` | Clears session and tokens |
| GET | `/auth/status` | Returns auth status and user info |
| POST | `/api/messages` | Agent endpoint (Activity Protocol) |

---

## Security Features

The application implements several security measures:

| Feature | Implementation |
|---------|----------------|
| **Token Encryption** | AES-256 encryption at rest using PBKDF2-derived keys from ClientSecret |
| **Session Cleanup** | Automatic TTL-based cleanup (8hr session lifetime, 15min cleanup interval) |
| **Prompt Injection Protection** | Input sanitization with XML delimiters and suspicious pattern detection |
| **CSRF Protection** | Cryptographically secure state parameter using `RandomNumberGenerator` |
| **Error Handling** | Generic error messages to users; detailed logging server-side only |
| **Swagger Protection** | API documentation only available in Development environment |
| **Thread-Safe Token Refresh** | Per-session `SemaphoreSlim` prevents concurrent refresh race conditions |

### Security Components

```
src/AgentOrchestrator/
├── Security/
│   └── InputSanitizer.cs       # Prompt injection protection
├── Auth/
│   ├── TokenService.cs         # Encrypted token cache with TTL
│   ├── AuthEndpoints.cs        # CSRF-protected OAuth flow
│   └── AuthMiddleware.cs       # Session validation, Swagger protection
```

---

## Configuration

### appsettings.json

```json
{
  "Connections": {
    "BotServiceConnection": {
      "Settings": {
        "ClientId": "<your-bot-client-id>",
        "ClientSecret": "<your-bot-client-secret>",
        "TenantId": "<your-tenant-id>"
      }
    }
  },
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "<your-tenant-id>",
    "ClientId": "<your-client-id>",
    "ClientSecret": "<your-client-secret>",
    "CallbackPath": "/auth/callback",
    "Scopes": ["openid", "profile", "email", "User.Read", "Mail.Read",
               "Calendars.Read", "Files.Read.All", "Sites.Read.All",
               "People.Read.All", "Chat.Read", "OnlineMeetingTranscript.Read.All",
               "ChannelMessage.Read.All", "ExternalItem.Read.All"]
  },
  "AzureOpenAI": {
    "Endpoint": "https://<your-resource>.openai.azure.com/",
    "ApiKey": "<your-api-key>",
    "DeploymentName": "gpt-4o"
  },
  "MicrosoftGraph": {
    "BaseUrl": "https://graph.microsoft.com/beta",
    "CopilotChatEndpoint": "/copilot/conversations"
  },
  "AgentApplication": {
    "UserAuthorization": {
      "DefaultHandlerName": "graph",
      "AutoSignIn": true,
      "Handlers": {
        "graph": {
          "Settings": {
            "AzureBotOAuthConnectionName": "GraphConnection"
          }
        }
      }
    }
  }
}
```

---

## Multi-Channel Support

The M365 Agents SDK enables deployment to multiple channels:

| Channel | Status | Notes |
|---------|--------|-------|
| **Web** | ✓ Implemented | Custom UI via `/api/messages` |
| **Teams** | Ready | Add Teams app manifest |
| **M365 Copilot** | Ready | Configure as Copilot plugin |
| **Slack** | Ready | Add Slack adapter |

---

## Benefits of This Architecture

1. **Multi-Channel Ready** - Same agent code works on Teams, M365 Copilot, Web, Slack
2. **Semantic Kernel Native** - Clean plugin architecture for AI functions
3. **Production Infrastructure** - Built-in state, storage, and conversation management
4. **M365 Copilot Integration** - Leverages enterprise data without RAG pipelines
5. **Future-Proof** - Microsoft 365 Agents SDK is actively maintained (GA status)

---

## Lab Modules

### Module 1: Setup & Authentication
- Configure Azure AD app registration
- Set up Azure OpenAI resource
- Run the application and authenticate

### Module 2: Understanding the Agent Architecture
- Explore OrchestratorAgent and activity handlers
- Understand Semantic Kernel plugin pattern
- Trace message flow end-to-end

### Module 3: M365 Copilot Chat API Integration
- Examine M365CopilotPlugin implementation
- Understand the Kiota-generated SDK
- Test Copilot queries for email, calendar, files

### Module 4: Multi-Intent Orchestration
- Test queries requiring multiple plugins
- Observe parallel execution
- Examine response synthesis

### Module 5: Extending the System (Optional)
- Add a new Semantic Kernel plugin
- Customize intent classification
- Deploy to additional channels

---

*Design Version: 2.0 (M365 Agents SDK)*
*Last Updated: January 2026*
