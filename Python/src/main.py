"""
FastAPI application for M365 multi-agent orchestrator.

Uses the M365 Agents SDK pattern with AgentApplication, CloudAdapter,
and start_agent_process for Activity Protocol support.

This mirrors the .NET Program.cs initialization pattern.
"""

import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI, Request
from microsoft_agents.hosting.core import (
    AgentApplication,
    TurnState,
)
from microsoft_agents.hosting.fastapi import (
    CloudAdapter,
    start_agent_process,
    JwtAuthorizationMiddleware,
)
from microsoft_agents.authentication.msal import MsalConnectionManager
from agent.orchestrator_agent import create_orchestrator_agent
from models.configuration import AppSettings
from config import load_settings

# Global agent application instance
AGENT_APP: AgentApplication[TurnState] = None
ADAPTER: CloudAdapter = None
APPSETTINGS: AppSettings = load_settings()
CONNECTION_MANAGER: MsalConnectionManager = MsalConnectionManager(**APPSETTINGS.agents_sdk_config)

@asynccontextmanager
async def lifespan(app: FastAPI):
    """
    Application lifespan handler for startup and shutdown.

    Mirrors the Program.cs initialization pattern.

    Args:
        app: FastAPI application instance.
    """
    # Startup
    global AGENT_APP, ADAPTER, APPSETTINGS, CONNECTION_MANAGER
    logging.info("Starting M365 Multi-Agent Orchestrator...")

    try:
        # Create orchestrator agent with M365 Agents SDK pattern
        AGENT_APP = create_orchestrator_agent(settings=APPSETTINGS, connection_manager=CONNECTION_MANAGER)
        ADAPTER = AGENT_APP._adapter

        logging.info("Orchestrator initialized successfully with M365 Agents SDK")
        logging.info(f"  - Max Agent Calls: {APPSETTINGS.orchestration.max_agent_calls}")
        logging.info(f"  - Timeout: {APPSETTINGS.orchestration.timeout_seconds}s")
        logging.info(f"  - Parallel Execution: {APPSETTINGS.orchestration.enable_parallel_execution}")

    except Exception as e:
        logging.error(f"Failed to initialize orchestrator: {e}")
        raise

    yield

    # Shutdown
    logging.info("Shutting down M365 Multi-Agent Orchestrator...")


# Create FastAPI app
# Mirrors: var builder = WebApplication.CreateBuilder(args) in Program.cs
app = FastAPI(
    title="M365 Multi-Agent Orchestrator",
    description="Multi-agent system integrating M365 Copilot with Azure OpenAI using M365 Agents SDK",
    version="1.0.0",
    lifespan=lifespan
)
app.state.agent_configuration = (
    CONNECTION_MANAGER.get_default_connection_configuration()
)

# Add JWT authorization middleware
app.add_middleware(JwtAuthorizationMiddleware)


@app.get("/api/messages")
async def health_check():
    """
    Health check endpoint.

    Mirrors: app.MapGet("/api/messages", ...) in Program.cs

    Returns:
        Status information.
    """
    return {
        "status": "healthy",
        "service": "M365 Multi-Agent Orchestrator",
        "sdk": "Microsoft Agents SDK for Python"
    }


@app.post("/api/messages")
async def messages_handler(request: Request):
    """
    Process incoming messages using the Activity Protocol.

    This follows the M365 Agents SDK pattern from empty_agent.py and mirrors
    the .NET endpoint: app.MapPost("/api/messages", ...) in Program.cs

    The start_agent_process function:
    1. Parses the Activity from the request
    2. Routes it to the appropriate handler based on activity type
    3. Returns the response in Activity Protocol format

    This replaces our previous custom JSON API with the standard Activity Protocol
    used by Teams, M365 Copilot, and other Microsoft agent platforms.

    Args:
        request: FastAPI request object containing Activity Protocol message.

    Returns:
        Activity Protocol response.
    """
    return await start_agent_process(
        request,
        AGENT_APP,
        ADAPTER,
    )


if __name__ == "__main__":
    # Configure logging
    # Mirrors: builder.Services.AddLogging() in Program.cs
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s - %(name)s - %(levelname)s - %(message)s"
    )

    # Run with uvicorn
    # Mirrors: app.Run() in Program.cs
    import uvicorn

    uvicorn.run(
        "main:app",
        host="0.0.0.0",
        port=3978,
        reload=False,
        log_level="info"
    )
