"""
Configuration models for the agent application.

Uses dataclasses for simple SDK compatibility.
"""

from dataclasses import dataclass
from typing import Optional


@dataclass
class AzureOpenAISettings:
    """Azure OpenAI service configuration."""

    endpoint: str
    api_key: str
    deployment_name: str
    api_version: str = "2024-08-01-preview"


@dataclass
class MicrosoftGraphSettings:
    """Microsoft Graph API configuration."""

    base_url: str = "https://graph.microsoft.com/beta"
    copilot_endpoint: str = "/copilot/conversations"


@dataclass
class OrchestrationSettings:
    """Orchestration behavior configuration."""

    max_agent_calls: int = 5
    timeout_seconds: int = 30
    enable_parallel_execution: bool = True


@dataclass
class ConnectionSettings:
    """Service connection configuration for M365 Agents SDK."""

    client_id: str
    client_secret: str
    tenant_id: str
    connection_name: str = "service_connection"


@dataclass
class AppSettings:
    """Complete application configuration."""

    azure_openai: AzureOpenAISettings
    microsoft_graph: MicrosoftGraphSettings
    orchestration: OrchestrationSettings
    connection: ConnectionSettings
    log_level: str = "INFO"
    agents_sdk_config: dict = None
