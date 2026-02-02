"""
Configuration loader for the agent application.

Loads settings from environment variables using the M365 Agents SDK pattern.
"""

import os
from typing import Optional
from dotenv import load_dotenv

from models.configuration import (
    AppSettings,
    AzureOpenAISettings,
    MicrosoftGraphSettings,
    OrchestrationSettings,
    ConnectionSettings
)
from microsoft_agents.activity import load_configuration_from_env


def load_settings(env_file: Optional[str] = None) -> AppSettings:
    """
    Load application settings from environment variables.

    Args:
        env_file: Optional path to .env file. If None, loads from .env in current directory.

    Returns:
        Complete application settings.

    Raises:
        ValueError: If required configuration is missing.
    """
    # Load .env file if specified or if default exists
    if env_file:
        load_dotenv(env_file)
    else:
        load_dotenv(os.path.join(os.path.dirname(__file__), ".env"))

    # Azure OpenAI settings
    azure_openai = AzureOpenAISettings(
        endpoint=_get_required_env("AZURE_OPENAI_ENDPOINT"),
        api_key=_get_required_env("AZURE_OPENAI_API_KEY"),
        deployment_name=_get_required_env("AZURE_OPENAI_DEPLOYMENT_NAME"),
        api_version=os.getenv("AZURE_OPENAI_API_VERSION", "2024-08-01-preview")
    )

    # Microsoft Graph settings
    microsoft_graph = MicrosoftGraphSettings(
        base_url=os.getenv("GRAPH_BASE_URL", "https://graph.microsoft.com/beta"),
        copilot_endpoint=os.getenv("GRAPH_COPILOT_ENDPOINT", "/copilot/conversations")
    )

    # Orchestration settings
    orchestration = OrchestrationSettings(
        max_agent_calls=int(os.getenv("ORCHESTRATION_MAX_AGENT_CALLS", "5")),
        timeout_seconds=int(os.getenv("ORCHESTRATION_TIMEOUT_SECONDS", "30")),
        enable_parallel_execution=os.getenv("ORCHESTRATION_ENABLE_PARALLEL_EXECUTION", "true").lower() == "true"
    )

    # Connection settings (M365 Agents SDK authentication)
    connection = ConnectionSettings(
        client_id=_get_required_env("CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTID"),
        client_secret=_get_required_env("CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTSECRET"),
        tenant_id=_get_required_env("CONNECTIONS__SERVICE_CONNECTION__SETTINGS__TENANTID"),
        connection_name=os.getenv("CONNECTION_NAME", "service_connection")
    )

    return AppSettings(
        azure_openai=azure_openai,
        microsoft_graph=microsoft_graph,
        orchestration=orchestration,
        connection=connection,
        log_level=os.getenv("LOG_LEVEL", "INFO"),
        agents_sdk_config=load_configuration_from_env(os.environ)
    )


def _get_required_env(key: str) -> str:
    """
    Get required environment variable or raise error.

    Args:
        key: Environment variable name.

    Returns:
        Environment variable value.

    Raises:
        ValueError: If environment variable is not set.
    """
    value = os.getenv(key)
    if value is None:
        raise ValueError(f"Required environment variable {key} is not set")
    return value
