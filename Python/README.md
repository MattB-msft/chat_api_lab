# M365 Multi-Agent Orchestrator - Python Port

Python port of the .NET 8 Microsoft 365 Agents SDK multi-agent orchestration system. Demonstrates integration of M365 Copilot Chat API with Azure OpenAI using FastAPI and Semantic Kernel.

## Overview

This system implements a 3-step orchestration pattern:

1. **Intent Analysis** - Classify user queries using Azure OpenAI
2. **Agent Execution** - Route to appropriate agents (M365 Copilot or Azure OpenAI)
3. **Response Synthesis** - Combine responses into coherent answer

### Key Features

- **Multi-Intent Support** - Handle queries with multiple topics
- **Parallel Execution** - Execute multiple agents concurrently
- **M365 Integration Ready** - Architecture prepared for M365 Copilot SDK
- **Configurable Orchestration** - Timeout, max calls, parallel/sequential execution

## Architecture

```
User Query
    ↓
Intent Classification (Azure OpenAI)
    ↓
┌─────────────────────────────────────┐
│  Agent Execution (Parallel/Serial)  │
├──────────────┬──────────────────────┤
│ M365 Copilot │   Azure OpenAI       │
│ - Emails     │   - General          │
│ - Calendar   │     Knowledge        │
│ - Files      │                      │
│ - People     │                      │
└──────────────┴──────────────────────┘
    ↓
Response Synthesis (Azure OpenAI)
    ↓
Final Response
```

## Quick Start

### Prerequisites

- Python 3.10+ (3.11+ recommended)
- Azure OpenAI resource with deployed model
- pip or poetry for dependency management

### Installation

1. **Clone and navigate to directory:**
   ```bash
   cd python/
   ```

2. **Install dependencies:**
   ```bash
   pip install -r requirements.txt
   ```

3. **Configure environment:**
   ```bash
   cp .env.example .env
   # Edit .env with your Azure OpenAI credentials
   ```

4. **Run the application:**
   ```bash
   python run.py
   ```

   The API will be available at `http://localhost:8000`

## Configuration

Edit `.env` file with your settings:

```bash
# Azure OpenAI (Required)
AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com/
AZURE_OPENAI_API_KEY=your-api-key
AZURE_OPENAI_DEPLOYMENT_NAME=gpt-4o-mini

# Orchestration Settings
ORCHESTRATION_MAX_AGENT_CALLS=5
ORCHESTRATION_TIMEOUT_SECONDS=30
ORCHESTRATION_ENABLE_PARALLEL_EXECUTION=true
```

For M365 integration, additional settings are required (see `.env.example`).

## Usage

### API Endpoints

#### Health Check
```bash
GET http://localhost:8000/api/messages
```

Response:
```json
{
  "status": "healthy",
  "service": "M365 Multi-Agent Orchestrator"
}
```

#### Process Message
```bash
POST http://localhost:8000/api/messages
Content-Type: application/json

{
  "text": "What is Docker?",
  "conversation_id": "optional-conversation-id"
}
```

Response:
```json
{
  "response": "Docker is a containerization platform...",
  "conversation_id": "generated-or-provided-id"
}
```

### Example Queries

**General Knowledge:**
```bash
curl -X POST http://localhost:8000/api/messages \
  -H "Content-Type: application/json" \
  -d '{"text": "What is Kubernetes?"}'
```

**Multi-Intent Query:**
```bash
curl -X POST http://localhost:8000/api/messages \
  -H "Content-Type: application/json" \
  -d '{"text": "What meetings do I have tomorrow and what is Docker?"}'
```

## Testing

Run tests with pytest:

```bash
# Run all tests
pytest

# Run with coverage
pytest --cov=src tests/

# Run specific test file
pytest tests/test_intent_plugin.py -v
```

## Project Structure

```
python/
├── src/
│   ├── agent/
│   │   └── orchestrator_agent.py       # Main orchestration logic
│   ├── plugins/
│   │   ├── intent_plugin.py            # Intent classification
│   │   ├── m365_copilot_plugin.py      # M365 Copilot integration
│   │   ├── azure_openai_plugin.py      # General knowledge
│   │   └── synthesis_plugin.py         # Response aggregation
│   ├── models/
│   │   ├── intent.py                   # Intent types and models
│   │   ├── agent_response.py           # Agent response model
│   │   └── configuration.py            # Settings classes
│   ├── state/
│   │   └── conversation_state.py       # Conversation storage
│   ├── constants/
│   │   └── plugin_names.py             # Plugin constants
│   ├── config.py                       # Configuration loader
│   └── main.py                         # FastAPI application
├── tests/
│   ├── conftest.py                     # Test fixtures
│   ├── test_models.py
│   ├── test_intent_plugin.py
│   └── test_orchestrator_agent.py
├── requirements.txt
├── .env.example
├── run.py
└── README.md
```

## Key Patterns

### Intent Classification

The system analyzes queries and identifies one or more intents:

- `M365Email` - Email-related queries
- `M365Calendar` - Calendar and meeting queries
- `M365Files` - Document and file queries
- `M365People` - People and organization queries
- `GeneralKnowledge` - General information queries

### Parallel Execution

When `ORCHESTRATION_ENABLE_PARALLEL_EXECUTION=true`, multiple agents execute concurrently using `asyncio.gather()`:

```python
tasks = [execute_agent(intent) for intent in intents]
responses = await asyncio.gather(*tasks)
```

### Timeout Handling

Requests timeout after configured seconds using `asyncio.timeout()`:

```python
async with asyncio.timeout(settings.orchestration.timeout_seconds):
    # Process request
```

### Conversation State

Conversation state persists across turns within the same conversation ID:

```python
state = await state_manager.get_state(conversation_id)
state["M365CopilotConversationId"] = conversation_id
await state_manager.set_state(conversation_id, state)
```

## M365 Integration Guide

This is a simplified version for demonstration. To integrate with actual M365 Copilot:

### 1. Install M365 Agents SDK

Uncomment in `requirements.txt`:
```
microsoft-agents-hosting-fastapi
microsoft-agents-hosting-core
microsoft-agents-authentication-msal
microsoft-agents-activity
microsoft-agents-m365copilot-beta
```

### 2. Configure Authentication

Add to `.env`:
```bash
CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTID=your-app-id
CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTSECRET=your-secret
CONNECTIONS__SERVICE_CONNECTION__SETTINGS__TENANTID=your-tenant-id
```

### 3. Update FastAPI Integration

See comments in `src/main.py` for full Activity Protocol integration with `start_agent_process()`.

### 4. Implement M365 Copilot SDK

See comments in `src/plugins/m365_copilot_plugin.py` for `AgentsM365CopilotBetaServiceClient` usage.

## Architecture Parity with .NET

This Python port maintains architectural parity with the .NET implementation:

| Component | .NET | Python |
|-----------|------|--------|
| Framework | M365 Agents SDK | FastAPI + M365 SDK (ready) |
| AI Orchestration | Semantic Kernel | Semantic Kernel |
| Intent Classification | Azure OpenAI | Azure OpenAI |
| M365 Integration | Kiota SDK | M365 Copilot SDK (ready) |
| Authentication | MSAL | MSAL (via SDK) |
| Async Patterns | Task/async-await | asyncio/async-await |

### Key Differences

1. **Syntax** - Python decorators vs C# attributes
2. **Import Style** - Underscores (`microsoft_agents`) vs namespaces
3. **Async** - `async def` vs `async Task<T>`

The patterns and architecture are identical.

## Development

### Adding New Plugins

1. Create plugin class inheriting base pattern
2. Use `@kernel_function` decorator for functions
3. Register in `orchestrator_agent.py`

Example:
```python
from semantic_kernel.functions import kernel_function

class CustomPlugin:
    @kernel_function(
        name="CustomFunction",
        description="Description for planner"
    )
    async def custom_function(self, query: str) -> str:
        # Implementation
        return result
```

### Configuration Management

Settings are loaded from environment variables via `src/config.py`. Override with:

- `.env` file for development
- Environment variables for production
- Azure Key Vault for secrets (production)

### Logging

Configure logging level in `.env`:
```bash
LOG_LEVEL=INFO  # DEBUG, INFO, WARNING, ERROR
```

## Production Considerations

Current implementation is simplified for demonstration:

### Needs Enhancement for Production:

1. **Session Storage** - Currently in-memory (use Redis)
2. **M365 Authentication** - Mock implementation (use real SDK)
3. **Error Handling** - Basic (add retry policies, circuit breakers)
4. **Monitoring** - Minimal (add Application Insights)
5. **Security** - Basic validation (add rate limiting, input sanitization)
6. **Token Management** - None (implement token caching)

See `// LAB SIMPLIFICATION:` comments in code for production guidance.

## Troubleshooting

### Import Errors
```bash
# Ensure src is in PYTHONPATH
export PYTHONPATH="${PYTHONPATH}:$(pwd)/src"
```

### Azure OpenAI Connection Issues
- Verify endpoint URL format (must end with `/`)
- Check API key is correct
- Ensure deployment name matches Azure resource

### Timeout Errors
- Increase `ORCHESTRATION_TIMEOUT_SECONDS`
- Check network connectivity to Azure
- Verify Azure OpenAI quota limits

## Related Documentation

- [.NET Implementation](../AgentOrchestrator/README.md)
- [Design Document](../DESIGN.md)
- [M365 Agents SDK for Python](https://github.com/microsoft/Agents-for-python)
- [Semantic Kernel Documentation](https://learn.microsoft.com/semantic-kernel)

## License

Same as parent project.
