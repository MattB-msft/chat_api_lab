# M365 Agents SDK Integration

## Overview

The Python implementation has been updated to use the **Microsoft 365 Agents SDK** pattern, matching the .NET implementation's architecture. This provides full Activity Protocol support and enables deployment to Microsoft Teams, M365 Copilot, and other platforms.

## What Changed

### Previous Implementation (Simple JSON API)
```python
# Old: Standalone orchestrator class
orchestrator = OrchestratorAgent(settings)
response = await orchestrator.process_message(user_message)
```

### Current Implementation (M365 Agents SDK)
```python
# New: AgentApplication with decorator-based handlers
AGENT_APP = AgentApplication[TurnState](storage=MemoryStorage())

@AGENT_APP.activity("message")
async def on_message(context: TurnContext, state: TurnState):
    # Process message with 3-step orchestration
    await orchestrator._process_message(context, state)

# Use start_agent_process for Activity Protocol
await start_agent_process(request, AGENT_APP, CLOUD_ADAPTER)
```

## Architecture Mapping: .NET → Python

| .NET (OrchestratorAgent.cs) | Python (orchestrator_agent.py) | Purpose |
|------------------------------|-------------------------------|---------|
| `public class OrchestratorAgent : AgentApplication` | `AGENT_APP = AgentApplication[TurnState]()` | Agent application instance |
| `OnActivity(ActivityTypes.Message, OnMessageActivityAsync, ...)` | `@AGENT_APP.activity("message")` | Message handler registration |
| `ITurnContext turnContext` | `TurnContext context` | Turn context for current request |
| `ITurnState turnState` | `TurnState state` | Conversation state |
| `MemoryStorage()` | `MemoryStorage()` | In-memory state storage |
| `AgentApplicationOptions` | Factory function pattern | Configuration |
| `/api/messages` endpoint | `/api/messages` with `start_agent_process()` | Activity Protocol endpoint |

## File Structure

### Core Implementation Files

**orchestrator_agent.py (540 lines)**
- `OrchestratorAgentApp` class - Manages AgentApplication and orchestration
- `_register_handlers()` - Registers activity handlers with decorators
- `_process_message()` - 3-step orchestration (mirrors OnMessageActivityAsync)
- `create_orchestrator_agent()` - Factory function to create AgentApplication

**main.py (154 lines)**
- FastAPI app with M365 Agents SDK integration
- CloudAdapter for Activity Protocol
- start_agent_process() for request handling
- JwtAuthorizationMiddleware for security

## Handler Registration Pattern

### .NET Pattern (OrchestratorAgent.cs:42-44)
```csharp
// Register activity handlers in constructor
OnActivity(ActivityTypes.ConversationUpdate, OnConversationUpdateAsync);
OnActivity(ActivityTypes.Message, OnMessageActivityAsync, isAgenticOnly: false, autoSignInHandlers: [NonAgenticAuthHandler]);
OnActivity(ActivityTypes.Message, OnMessageActivityAsync, isAgenticOnly: true, autoSignInHandlers: [AgenticAuthHandler]);
```

### Python Pattern (orchestrator_agent.py:78-126)
```python
def _register_handlers(self):
    """Register activity handlers with the agent application."""

    # Handle conversation updates (e.g., user joins)
    @self._agent_app.conversation_update("membersAdded")
    async def on_conversation_update(context: TurnContext, state: TurnState):
        await context.send_activity("Welcome message")

    # Handle message activities (main orchestration)
    @self._agent_app.activity("message")
    async def on_message_activity(context: TurnContext, state: TurnState):
        await self._process_message(context, state)

    # Handle help command
    @self._agent_app.message("/help")
    async def on_help(context: TurnContext, state: TurnState):
        await context.send_activity("Help text")
```

## Activity Protocol Support

### What is the Activity Protocol?

The Activity Protocol is Microsoft's standard format for agent communication used by:
- Microsoft Teams bots
- M365 Copilot plugins
- Azure Bot Service
- Other Microsoft conversation platforms

### Request Format
```json
{
  "type": "message",
  "text": "What is Docker?",
  "from": {
    "id": "user123",
    "name": "John Doe"
  },
  "conversation": {
    "id": "conversation123"
  },
  "channelId": "msteams",
  "serviceUrl": "https://smba.trafficmanager.net/amer/"
}
```

### Response Format
```json
{
  "type": "message",
  "text": "Docker is a containerization platform...",
  "from": {
    "id": "bot123",
    "name": "M365 Orchestrator"
  }
}
```

## Key Components

### 1. AgentApplication[TurnState]

The core agent application instance that manages:
- **Storage**: MemoryStorage for conversation state
- **Adapters**: CloudAdapter for cloud-based deployments
- **Handlers**: Decorated functions for different activity types

```python
AGENT_APP = AgentApplication[TurnState](
    storage=MemoryStorage()
)
```

### 2. CloudAdapter

Handles Activity Protocol serialization/deserialization:
- Parses incoming Activity objects from HTTP requests
- Serializes responses back to Activity format
- Manages authentication and security

```python
CLOUD_ADAPTER = CloudAdapter()
```

### 3. start_agent_process()

Routes Activity Protocol requests to appropriate handlers:
- Parses Activity from FastAPI Request
- Looks up handler based on activity type
- Invokes handler with TurnContext and TurnState
- Returns Activity Protocol response

```python
return await start_agent_process(
    request,
    AGENT_APP,
    CLOUD_ADAPTER,
)
```

### 4. TurnContext and TurnState

**TurnContext**: Request-scoped context
- `context.activity` - The incoming Activity
- `context.activity.text` - User message text
- `context.send_activity()` - Send response
- Mirrors .NET `ITurnContext`

**TurnState**: Conversation state storage
- `state.conversation` - Dictionary for conversation data
- Persists across turns within same conversation
- Mirrors .NET `ITurnState`

## Orchestration Flow

### 1. Request Arrives
```
HTTP POST /api/messages
↓
FastAPI messages_handler()
↓
start_agent_process(request, AGENT_APP, CLOUD_ADAPTER)
```

### 2. Activity Routing
```
CloudAdapter parses Activity
↓
Looks up handler: @AGENT_APP.activity("message")
↓
Invokes: on_message_activity(context, state)
```

### 3. Orchestration (3 Steps)
```
_process_message(context, state)
↓
1. Analyze Intent (Azure OpenAI)
↓
2. Execute Agents (M365 Copilot + Azure OpenAI)
↓
3. Synthesize Response
↓
context.send_activity(final_response)
```

### 4. Response
```
CloudAdapter serializes Activity response
↓
FastAPI returns HTTP response
```

## State Management

### Conversation State Pattern

**.NET Pattern (OrchestratorAgent.cs:113, 130)**
```csharp
// Get from state
string? conversationId = _context.State.Conversation.GetValue<string>("M365CopilotConversationId");

// Save to state
_context.State.Conversation.SetValue<string>("M365CopilotConversationId", conversationId);
```

**Python Pattern (orchestrator_agent.py:219-223, 276-278)**
```python
# Get from state
conversation_state = {}
if hasattr(state, 'conversation') and state.conversation:
    conversation_state = dict(state.conversation)

# Save to state
if hasattr(state, 'conversation'):
    for key, value in agent_context.conversation_state.items():
        state.conversation[key] = value
```

## Dependencies

### Required M365 Agents SDK Packages

```txt
# M365 Agents SDK packages (now integrated)
microsoft-agents-hosting-fastapi    # FastAPI adapter
microsoft-agents-hosting-core       # Core AgentApplication
microsoft-agents-authentication-msal # Authentication
microsoft-agents-activity           # Activity Protocol models
```

### Future M365 Copilot Integration

```txt
# M365 Copilot SDK (for future M365 Copilot integration)
# microsoft-agents-m365copilot-beta
# microsoft-agents-m365copilot-core
```

## Testing with Activity Protocol

### Using curl

```bash
# Send Activity Protocol message
curl -X POST http://localhost:8000/api/messages \
  -H "Content-Type: application/json" \
  -d '{
    "type": "message",
    "text": "What is Docker?",
    "from": {"id": "user1"},
    "conversation": {"id": "conv1"}
  }'
```

### Expected Response

```json
{
  "type": "message",
  "text": "Docker is a containerization platform that allows developers to package applications and their dependencies into containers...",
  "from": {"id": "bot"},
  "conversation": {"id": "conv1"}
}
```

## Benefits of M365 Agents SDK Integration

### 1. **Platform Compatibility**
- ✅ Works with Microsoft Teams
- ✅ Works with M365 Copilot
- ✅ Works with Azure Bot Service
- ✅ Works with any Activity Protocol platform

### 2. **Standard Protocol**
- ✅ Activity Protocol is industry standard
- ✅ Well-documented and supported
- ✅ Rich features (attachments, cards, actions)

### 3. **Authentication Built-in**
- ✅ JWT validation middleware
- ✅ MSAL integration ready
- ✅ Token exchange patterns

### 4. **State Management**
- ✅ Built-in conversation state
- ✅ Automatic serialization
- ✅ Pluggable storage (Memory, Redis, SQL)

### 5. **Code Parity with .NET**
- ✅ Same architecture patterns
- ✅ Similar APIs and conventions
- ✅ Easier cross-platform maintenance

## Migration from Simple JSON API

If you have existing code using the old simple JSON API:

### Old API (No longer supported)
```bash
curl -X POST http://localhost:8000/api/messages \
  -H "Content-Type: application/json" \
  -d '{"text": "What is Docker?"}'
```

### New Activity Protocol API
```bash
curl -X POST http://localhost:8000/api/messages \
  -H "Content-Type: application/json" \
  -d '{
    "type": "message",
    "text": "What is Docker?",
    "from": {"id": "user1"},
    "conversation": {"id": "conv1"}
  }'
```

### Migration Steps

1. **Update request format** - Add Activity Protocol fields
2. **Parse responses** - Extract text from Activity response
3. **Handle conversation IDs** - Use Activity conversation.id
4. **Update error handling** - Activity Protocol error responses

## Next Steps

### 1. Add Authentication

Uncomment and configure in `main.py`:

```python
from microsoft_agents.authentication.msal import (
    MsalConnectionManager,
    Authorization
)

CONNECTION_MANAGER = MsalConnectionManager(
    client_id=settings.connection.client_id,
    client_secret=settings.connection.client_secret,
    tenant_id=settings.connection.tenant_id
)

AUTHORIZATION = Authorization(
    storage=STORAGE,
    connection_manager=CONNECTION_MANAGER
)
```

### 2. Enable M365 Copilot SDK

1. Uncomment in requirements.txt:
   ```
   microsoft-agents-m365copilot-beta
   ```

2. Update `m365_copilot_plugin.py` to use real SDK:
   ```python
   from microsoft_agents.m365copilot.beta import AgentsM365CopilotBetaServiceClient
   ```

3. Implement token acquisition in handlers

### 3. Deploy to Azure

- Use Azure App Service for hosting
- Configure environment variables
- Enable authentication
- Set up monitoring

### 4. Deploy to Teams

- Create Teams app manifest
- Configure bot registration
- Test in Teams client

## Troubleshooting

### Import Errors

```bash
# Install M365 Agents SDK packages
pip install microsoft-agents-hosting-fastapi microsoft-agents-hosting-core
```

### Activity Protocol Parsing Errors

- Ensure request has required fields: `type`, `from`, `conversation`
- Check Content-Type header is `application/json`
- Validate Activity format matches specification

### Handler Not Invoked

- Check decorator syntax: `@AGENT_APP.activity("message")`
- Ensure handlers are registered before app starts
- Verify activity type matches handler

## Reference Documentation

- **M365 Agents SDK for Python**: https://github.com/microsoft/Agents-for-python
- **Activity Protocol Spec**: https://aka.ms/botframework-activity-schema
- **.NET OrchestratorAgent**: `../AgentOrchestrator/src/Agent/OrchestratorAgent.cs`
- **FastAPI Integration Example**: `empty_agent.py` in SDK repo

## Conclusion

The Python implementation now uses the M365 Agents SDK AgentApplication pattern, providing full architectural parity with the .NET implementation and enabling deployment to Microsoft Teams, M365 Copilot, and other platforms through the standard Activity Protocol.
