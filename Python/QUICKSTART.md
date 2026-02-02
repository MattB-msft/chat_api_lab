# Quick Start Guide

Get the M365 Multi-Agent Orchestrator running in 5 minutes.

## Prerequisites

- Python 3.10 or higher
- Azure OpenAI resource with a deployed model
- pip package manager

## Steps

### 1. Navigate to Project

```bash
cd python/
```

### 2. Install Dependencies

```bash
pip install -r requirements.txt
```

Expected output:
```
Installing collected packages: fastapi, uvicorn, semantic-kernel, openai, python-dotenv...
Successfully installed...
```

### 3. Configure Environment

```bash
# Copy the example file
cp .env.example .env

# Edit .env with your editor
# At minimum, set these three values:
# AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com/
# AZURE_OPENAI_API_KEY=your-key-here
# AZURE_OPENAI_DEPLOYMENT_NAME=gpt-4o-mini
```

**Getting Azure OpenAI Credentials:**

1. Go to [Azure Portal](https://portal.azure.com)
2. Navigate to your Azure OpenAI resource
3. Click "Keys and Endpoint" in the left menu
4. Copy:
   - **Endpoint** → `AZURE_OPENAI_ENDPOINT`
   - **Key 1** → `AZURE_OPENAI_API_KEY`
5. Go to "Model deployments" and note your deployment name → `AZURE_OPENAI_DEPLOYMENT_NAME`

### 4. Verify Configuration

```bash
python -c "import sys; sys.path.insert(0, 'src'); from config import load_settings; settings = load_settings(); print(f'Configuration loaded: {settings.azure_openai.deployment_name}')"
```

Expected output:
```
Configuration loaded: gpt-4o-mini
```

### 5. Run the Server

```bash
python run.py
```

Expected output:
```
INFO:     Started server process [12345]
INFO:     Waiting for application startup.
INFO:     Starting M365 Multi-Agent Orchestrator...
INFO:     Orchestrator initialized successfully
INFO:     Application startup complete.
INFO:     Uvicorn running on http://0.0.0.0:8000
```

### 6. Test the API

Open a new terminal and test:

```bash
# Health check
curl http://localhost:8000/api/messages

# Process a message
curl -X POST http://localhost:8000/api/messages \
  -H "Content-Type: application/json" \
  -d '{"text": "What is Docker?"}'
```

Expected response:
```json
{
  "response": "Docker is a containerization platform that allows developers to package applications...",
  "conversation_id": "generated-uuid"
}
```

## Example Queries

### General Knowledge
```bash
curl -X POST http://localhost:8000/api/messages \
  -H "Content-Type: application/json" \
  -d '{"text": "Explain Kubernetes in simple terms"}'
```

### Multi-Intent Query
```bash
curl -X POST http://localhost:8000/api/messages \
  -H "Content-Type: application/json" \
  -d '{"text": "What meetings do I have tomorrow and what is REST API?"}'
```

### With Conversation ID
```bash
curl -X POST http://localhost:8000/api/messages \
  -H "Content-Type: application/json" \
  -d '{"text": "Tell me more", "conversation_id": "my-conversation-123"}'
```

## Testing

Run the test suite:

```bash
pytest tests/ -v
```

Expected output:
```
tests/test_models.py::TestIntent::test_intent_creation PASSED
tests/test_models.py::TestIntent::test_intent_is_m365_intent_true PASSED
...
========================= X passed in 0.5s =========================
```

## Troubleshooting

### Import Error: No module named 'src'

**Solution:** Ensure you're running commands from the `python/` directory.

### Configuration Error: Required environment variable not set

**Solution:** Check your `.env` file has all required values:
```bash
cat .env | grep AZURE_OPENAI
```

### Connection Error: Failed to connect to Azure OpenAI

**Solutions:**
- Verify endpoint URL ends with `/`
- Check API key is correct (no extra spaces)
- Ensure deployment name matches your Azure resource
- Test connectivity: `curl https://your-endpoint.openai.azure.com`

### Timeout Error: Request timed out

**Solutions:**
- Increase timeout: `ORCHESTRATION_TIMEOUT_SECONDS=60` in `.env`
- Check Azure OpenAI quota limits in Azure Portal
- Verify network connectivity

## What's Next?

### Run Tests
```bash
pytest tests/ -v --cov=src
```

### Enable Debug Logging
```bash
# In .env
LOG_LEVEL=DEBUG
```

### Configure Orchestration
```bash
# In .env
ORCHESTRATION_MAX_AGENT_CALLS=10
ORCHESTRATION_TIMEOUT_SECONDS=60
ORCHESTRATION_ENABLE_PARALLEL_EXECUTION=true
```

### Read Documentation
- [README.md](README.md) - Full documentation
- [IMPLEMENTATION_SUMMARY.md](IMPLEMENTATION_SUMMARY.md) - Implementation details
- [.env.example](.env.example) - All configuration options

## Getting Help

If you encounter issues:

1. Check [TROUBLESHOOTING.md](../AgentOrchestrator/docs/self-paced/TROUBLESHOOTING.md) in the .NET project
2. Verify configuration with: `python -c "import sys; sys.path.insert(0, 'src'); from config import load_settings; load_settings()"`
3. Check server logs for error details
4. Review [IMPLEMENTATION_SUMMARY.md](IMPLEMENTATION_SUMMARY.md) for architecture details

## Success Checklist

- [ ] Python 3.10+ installed
- [ ] Dependencies installed (`pip install -r requirements.txt`)
- [ ] `.env` configured with Azure OpenAI credentials
- [ ] Configuration loads without errors
- [ ] Server starts successfully
- [ ] Health check returns `{"status": "healthy"}`
- [ ] Message processing returns a response
- [ ] Tests pass

Once all items are checked, you're ready to explore the multi-agent orchestration system!
