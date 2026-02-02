"""
Pytest configuration and fixtures for tests.
"""

import pytest
import logging
from unittest.mock import Mock, AsyncMock

from semantic_kernel import Kernel
from semantic_kernel.connectors.ai.open_ai import AzureChatCompletion

from models.configuration import (
    AppSettings,
    AzureOpenAISettings,
    MicrosoftGraphSettings,
    OrchestrationSettings,
    ConnectionSettings
)
from plugins.agent_context import AgentContext


@pytest.fixture
def test_settings() -> AppSettings:
    """
    Create test application settings.

    Returns:
        Test configuration.
    """
    return AppSettings(
        azure_openai=AzureOpenAISettings(
            endpoint="https://test.openai.azure.com/",
            api_key="test-api-key",
            deployment_name="gpt-4o-mini",
            api_version="2024-08-01-preview"
        ),
        microsoft_graph=MicrosoftGraphSettings(
            base_url="https://graph.microsoft.com/beta",
            copilot_endpoint="/copilot/conversations"
        ),
        orchestration=OrchestrationSettings(
            max_agent_calls=5,
            timeout_seconds=30,
            enable_parallel_execution=True
        ),
        connection=ConnectionSettings(
            client_id="test-client-id",
            client_secret="test-client-secret",
            tenant_id="test-tenant-id"
        ),
        log_level="INFO"
    )


@pytest.fixture
def mock_kernel() -> Kernel:
    """
    Create mock Semantic Kernel for testing.

    Returns:
        Mock kernel instance.
    """
    kernel = Kernel()

    # Add mock Azure OpenAI service with proper service_id attribute
    mock_service = Mock(spec=AzureChatCompletion)
    mock_service.service_id = "test-service"
    mock_service.ai_model_id = "test-model"
    kernel.add_service(mock_service)

    return kernel


@pytest.fixture
def mock_context() -> AgentContext:
    """
    Create mock agent context for testing.

    Returns:
        Mock agent context.
    """
    return AgentContext(
        request_id="test-request-id",
        user_message="test message",
        conversation_state={},
        access_token="test-token",
        logger=logging.getLogger("test")
    )


@pytest.fixture
def mock_kernel_invoke() -> AsyncMock:
    """
    Create mock for kernel.invoke method.

    Returns:
        AsyncMock for kernel invoke.
    """
    return AsyncMock()
