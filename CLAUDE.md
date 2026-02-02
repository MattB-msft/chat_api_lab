# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a .NET 8 multi-agent orchestration lab demonstrating how to integrate Microsoft 365 Copilot Chat API with Azure OpenAI using the Microsoft 365 Agents SDK and Semantic Kernel. The agent analyzes user intent, routes M365-related queries to Copilot, and synthesizes responses from multiple AI sources.

**Key Value**: Shows how to use M365 Copilot as a specialist agent for enterprise data (emails, calendar, files, people) without building custom RAG pipelines.

## Build & Development Commands

### Build
```bash
cd AgentOrchestrator
dotnet build src/AgentOrchestrator.csproj
```

### Run
```bash
cd AgentOrchestrator
dotnet run --project src/AgentOrchestrator.csproj
# App runs on http://localhost:3978
```

### Test
```bash
cd AgentOrchestrator
dotnet test tests/AgentOrchestrator.Tests/AgentOrchestrator.Tests.csproj
```

Run specific test:
```bash
dotnet test --filter "FullyQualifiedName~IntentPluginTests"
```

### Clean
```bash
cd AgentOrchestrator
dotnet clean
```

## Architecture Overview

### Multi-Agent Orchestration Flow

1. **User Message** → OrchestratorAgent receives via Activity Protocol
2. **Intent Analysis** → IntentPlugin classifies query (M365Email, M365Calendar, M365Files, M365People, GeneralKnowledge)
3. **Parallel Execution** → Multiple plugins execute concurrently based on intents
4. **Response Synthesis** → SynthesisPlugin combines responses into coherent answer

### Core Components

**OrchestratorAgent** (AgentOrchestrator/src/Agent/OrchestratorAgent.cs:14)
- Extends `AgentApplication` from M365 Agents SDK
- Main entry point: `OnMessageActivityAsync` handles incoming messages
- Orchestrates the 3-step flow: analyze intent → execute agents → synthesize response
- Supports both agentic (agent-to-agent) and non-agentic (user) authentication flows

**Semantic Kernel Plugins** (AgentOrchestrator/src/Plugins/)
- `IntentPlugin` - Classifies user intent using Azure OpenAI
- `M365CopilotPlugin` - Queries M365 data via Copilot Chat API (beta endpoint: /beta/copilot/conversations)
- `AzureOpenAIPlugin` - Handles general knowledge queries
- `SynthesisPlugin` - Combines multiple agent responses

**Plugin Registration**
- Plugins are registered per-turn in `UpdateChatClientWithToolsInContext` (OrchestratorAgent.cs:361)
- Each plugin receives `AgentContext` with turn context, state, and user authorization
- Functions marked with `[KernelFunction]` and `[Description]` attributes

### M365 Copilot Chat API Integration

Two-step conversation pattern:
1. Create conversation: `POST /beta/copilot/conversations` → returns conversationId
2. Send chat message: `POST /beta/copilot/conversations/{id}/chat` → returns response

Implementation uses Kiota-generated SDK from `Microsoft.Agents.M365Copilot.Beta` package.

**Conversation State**: ConversationId is stored in turn state (`M365CopilotConversationId`) and reused within the same conversation.

### Authentication

Two authentication handlers:
- `"agentic"` - For agent-to-agent communication using AgentID flows
- `"me"` - For user On-Behalf-Of (OBO) flows

Required Graph API scopes: `User.Read`, `Mail.Read`, `Calendars.Read`, `Files.Read.All`, `Sites.Read.All`, `People.Read.All`, `Chat.Read`, `OnlineMeetingTranscript.Read.All`, `ChannelMessage.Read.All`, `ExternalItem.Read.All`

Token exchange happens in plugins via `_context.UserAuth.ExchangeTurnTokenAsync()` (M365CopilotPlugin.cs:204)

## Configuration

Configuration is loaded from `appsettings.json` and should be overridden with:
- **Development**: `dotnet user-secrets`
- **Production**: Azure Key Vault or environment variables

Key sections:
- `AIServices:AzureOpenAI` - Azure OpenAI endpoint, API key, deployment name
- `MicrosoftGraph` - Graph API base URL and Copilot endpoint
- `Orchestration` - MaxAgentCalls (default: 5), TimeoutSeconds (default: 30), EnableParallelExecution (default: true)
- `TokenValidation` - Audiences for bot token validation
- `AgentApplication` - M365 Agents SDK settings including UserAuthorization handlers
- `Connections` - Bot Service connection configuration with MSAL authentication

## Deployment with Microsoft Agents Toolkit

This project uses the Microsoft Agents Toolkit (formerly Teams Toolkit) for provisioning Azure resources and deploying to Teams and Azure App Service.

### Environment File Configuration

The toolkit uses environment files in the `AgentOrchestrator/env/` directory to manage configuration across different environments.

#### Step 1: Configure Local Environment Files

1. **Copy the sample files and remove `.sample` extension:**
   ```bash
   cd AgentOrchestrator/env
   cp .env.local.sample .env.local
   cp .env.local.user.sample .env.local.user
   ```

2. **Edit `.env.local`** - Public configuration (can be committed to git):
   ```bash
   # App display name shown in Teams
   APP_DISPLAY_NAME=M365ChatAPI Multi-Agent

   # Internal app name (no spaces, lowercase)
   APP_INTERNAL_NAME=m365chatapi-multi-agent

   # Environment name
   TEAMSFX_ENV=local
   APP_NAME_SUFFIX=local

   # Azure OpenAI endpoint (replace with your Azure OpenAI resource)
   MODELS_ENDPOINT=https://your-resource.openai.azure.com/

   # Model deployment name in Azure OpenAI
   LANGUAGE_MODEL_NAME=gpt-4o-mini

   # Generated during provision (leave as underscore initially)
   BOT_SERVICE_PRINCIPAL_ID=_
   ```

3. **Edit `.env.local.user`** - Secrets (gitignored, never commit):
   ```bash
   # Azure OpenAI API key (get from Azure Portal > your OpenAI resource > Keys and Endpoint)
   SECRET_MODELS_API_KEY=your-azure-openai-api-key-here
   ```

#### Step 2: Configure Azure Subscription (for cloud deployment)

For deploying to Azure (not just local dev), you'll also need to create production environment files:

1. **Copy and configure `.env.dev` files:**
   ```bash
   cd AgentOrchestrator/env
   # Create files based on the .local versions
   cp .env.local .env.dev
   cp .env.local.user .env.dev.user
   ```

2. **Edit `.env.dev` to add Azure configuration:**
   ```bash
   # All settings from .env.local, plus:

   # Azure subscription ID (get from Azure Portal)
   AZURE_SUBSCRIPTION_ID=your-subscription-id

   # Azure resource group name (will be created if doesn't exist)
   AZURE_RESOURCE_GROUP_NAME=rg-m365agent-dev

   # Unique suffix for Azure resources (keeps names unique)
   RESOURCE_SUFFIX=abc123
   ```

3. **Edit `.env.dev.user` with the same API key:**
   ```bash
   SECRET_MODELS_API_KEY=your-azure-openai-api-key-here
   ```

### Toolkit Workflow Files

The Microsoft Agents Toolkit uses these YAML files for orchestration:

- **`m365agents.local.yml`** - Local development with dev tunnel (no Azure deployment)
  - Creates Teams app registration
  - Creates Azure AD app with client secret
  - Provisions local bot configuration
  - Generates `appsettings.local.json` with all settings

- **`m365agents.yml`** - Azure deployment
  - Provisions Azure resources via Bicep templates (App Service, Storage, Key Vault, Monitoring)
  - Deploys to Azure App Service
  - Configures Teams app package
  - Generates `appsettings.json` with all settings

### Deployment Commands

These commands are typically run from VS Code with the Microsoft Agents Toolkit extension, but can also be run via CLI:
More information on the Agent Toolkit CLI can be found here: [Microsoft Agents Toolkit CLI](https://learn.microsoft.com/en-us/microsoftteams/platform/toolkit/microsoft-365-agents-toolkit-cli)

**Provision resources (local development):**
```bash
cd AgentOrchestrator
# Uses m365agents.local.yml and env/.env.local
atk provision --env local
```

**Provision and deploy to Azure:**
```bash
cd AgentOrchestrator
# Uses m365agents.yml and env/.env.dev
atk provision --env dev
atk deploy --env dev
```

### What Gets Provisioned

**Local Development** (`m365agents.local.yml`):
- Teams app registration
- Azure AD app with client secret for bot authentication
- Azure AD app permissions for Graph API
- Service principal for bot identity
- Local configuration in `src/appsettings.local.json`

**Azure Deployment** (`m365agents.yml`):
- **App Service** (B1 SKU) - Hosts the .NET application
- **Storage Account** - For bot conversation state
- **Key Vault** - Stores secrets (API keys, connection strings)
- **Application Insights** - Monitoring and diagnostics
- **Log Analytics Workspace** - Centralized logging
- **Managed Identity** - Secure access to Azure resources
- Local configuration in `src/appsettings.json`

### Generated Configuration Files

After provisioning, the toolkit generates:

**`src/appsettings.local.json`** - Complete runtime configuration with:
- Bot authentication settings (client ID, secret, tenant)
- Azure OpenAI configuration
- M365 Copilot API settings
- UserAuthorization handlers (agentic and "me")
- Connection strings and endpoints

This file is auto-generated from the template in `m365agents.local.yml` (lines 47-102) or `m365agents.yml` (lines 26-83).

### Environment Variables Reference

| Variable | Source File | Description | Example |
|----------|-------------|-------------|---------|
| `APP_DISPLAY_NAME` | .env.local | Display name in Teams | `M365ChatAPI Multi-Agent` |
| `APP_INTERNAL_NAME` | .env.local | Internal identifier | `m365chatapi-multi-agent` |
| `MODELS_ENDPOINT` | .env.local | Azure OpenAI endpoint | `https://myai.openai.azure.com/` |
| `LANGUAGE_MODEL_NAME` | .env.local | Deployment name | `gpt-4o-mini` |
| `SECRET_MODELS_API_KEY` | .env.local.user | Azure OpenAI API key | `abc123...` (secret) |
| `AZURE_SUBSCRIPTION_ID` | .env.dev | Azure subscription | `xxxxxxxx-xxxx-...` |
| `AZURE_RESOURCE_GROUP_NAME` | .env.dev | Resource group | `rg-m365agent-dev` |
| `RESOURCE_SUFFIX` | .env.dev | Unique suffix | `abc123` |
| `BOT_ID` | (generated) | Bot client ID | Generated by toolkit |
| `SECRET_BOT_PASSWORD` | (generated) | Bot client secret | Generated by toolkit |
| `BOT_TENANT_ID` | (generated) | Tenant ID | Generated by toolkit |

### Prerequisites for Deployment

Before running provision/deploy:

1. **Microsoft 365 tenant** with admin access
2. **Azure subscription** with Contributor role
3. **Azure OpenAI resource** already created with model deployed
4. **Azure CLI** installed and authenticated: `az login`
5. **Microsoft 365 Agents Toolkit CLI** or VS Code extension

### Typical Development Workflow

1. Configure `.env.local` and `.env.local.user` with your Azure OpenAI settings
2. Run `atk provision --env local` to create bot registration
3. Run `dotnet run` to start the agent locally
4. Test in Teams using the dev tunnel URL
5. When ready for cloud: Configure `.env.dev` files and run provision/deploy to Azure

## Important Patterns

### Session & State Management

- Uses in-memory session storage (`AddDistributedMemoryCache()`) - suitable for single-instance development only
- For production: Use Redis or SQL Server distributed cache
- Conversation state uses `MemoryStorage` - lost on restart (Program.cs:125)

### Resilience

HTTP client configured with standard resilience handler (Program.cs:82):

- Retry: 3 attempts with 1s delay
- Circuit breaker with 640s sampling duration
- Timeout: 120s for Copilot API calls (they can take 10-30 seconds)

### Rate Limiting

Fixed window rate limiter: 30 requests/minute per IP with 5 request queue (Program.cs:145)

### Parallel Execution

When `EnableParallelExecution: true`, intents execute concurrently via `Task.WhenAll` (OrchestratorAgent.cs:176)

### Error Handling

- Timeouts create linked cancellation token with configured timeout (OrchestratorAgent.cs:80)
- Intent parsing failures fallback to GeneralKnowledge intent (OrchestratorAgent.cs:158)
- Plugin execution errors return AgentResponse with Success=false (OrchestratorAgent.cs:218)

## Security Notes

This codebase contains educational markers:

- `// SECURITY:` - Security best practices to understand
- `// LAB SIMPLIFICATION:` - Patterns simplified for learning
- `// PRODUCTION:` - What would differ in production

Key simplifications:

- Secrets in appsettings.json (use Key Vault in production)
- In-memory token cache (use Redis in production)
- HTTP instead of HTTPS (use HTTPS in production)
- In-memory session storage (use distributed cache in production)

## Technology Stack

- **.NET 8** - Target framework
- **Microsoft 365 Agents SDK 1.4** - Agent framework with multi-channel support
- **Semantic Kernel 1.54** - AI orchestration and plugin pattern
- **Azure OpenAI** - GPT-4o for intent analysis and general knowledge
- **Microsoft Graph Copilot Chat API** - Beta endpoint for M365 data
- **Kiota** - SDK generation for Graph API
- **MSAL** - Microsoft Authentication Library
- **xUnit** - Test framework with Moq for mocking

## Project Structure

```
AgentOrchestrator/
├── src/
│   ├── Agent/
│   │   └── OrchestratorAgent.cs       # Main agent orchestration logic
│   ├── Plugins/                       # Semantic Kernel plugins
│   │   ├── AgentContext.cs            # Context passed to plugins
│   │   ├── IntentPlugin.cs            # Intent classification
│   │   ├── M365CopilotPlugin.cs       # M365 Copilot integration
│   │   ├── AzureOpenAIPlugin.cs       # General knowledge
│   │   └── SynthesisPlugin.cs         # Response aggregation
│   ├── Models/                        # Data models and configuration
│   │   ├── Configuration.cs           # Settings classes
│   │   ├── Intent.cs                  # Intent types
│   │   └── AgentResponse.cs           # Agent response model
│   ├── Constants/                     # Plugin names and constants
│   ├── Program.cs                     # DI, middleware, agent registration
│   └── appsettings.json               # Configuration template
├── tests/
│   └── AgentOrchestrator.Tests/       # xUnit tests
├── appPackage/                        # Teams app manifest
└── infra/                             # Infrastructure scripts
```

## Multi-Channel Support

The M365 Agents SDK enables deployment to:

- **Web** - Custom UI via `/api/messages` endpoint (implemented)
- **Teams** - Add Teams app manifest from appPackage/
- **M365 Copilot** - Configure as Copilot plugin
- **Slack** - Add Slack adapter

All use the same Activity Protocol and agent code.

## Additional Resources

- [Design Document](DESIGN.md) - Detailed architecture
- [Lab Guide](docs/self-paced/LAB_GUIDE.md) - Step-by-step instructions
- [Troubleshooting](docs/self-paced/TROUBLESHOOTING.md) - Common issues
