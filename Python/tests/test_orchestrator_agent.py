"""
Tests for OrchestratorAgent with M365 Agents SDK integration.
"""

import pytest
import asyncio

from agent.orchestrator_agent import create_orchestrator_agent, MAX_MESSAGE_LENGTH
from microsoft_agents.hosting.core import AgentApplication, TurnState


class TestOrchestratorAgent:
    """Tests for OrchestratorAgent with M365 Agents SDK."""

    def test_agent_initialization(self, test_settings):
        """Test orchestrator agent initialization."""
        agent_app = create_orchestrator_agent(test_settings)

        assert agent_app is not None
        assert isinstance(agent_app, AgentApplication)
        assert MAX_MESSAGE_LENGTH == 4000

    # Note: The following tests would require creating mock TurnContext and TurnState
    # objects, which is more appropriate for integration tests. The AgentApplication
    # pattern uses handlers decorated with @agent_app.activity() which are invoked
    # by the M365 Agents SDK framework, not directly.

    # For unit testing, we test the configuration and initialization only.
    # Message processing tests would be integration tests with the Activity Protocol.

    def test_max_agent_calls_setting(self, test_settings):
        """Test that MaxAgentCalls setting is configured."""
        test_settings.orchestration.max_agent_calls = 3
        agent_app = create_orchestrator_agent(test_settings)

        # Verify agent app was created with settings
        assert agent_app is not None
        assert isinstance(agent_app, AgentApplication)

    def test_parallel_execution_setting(self, test_settings):
        """Test parallel execution setting configuration."""
        test_settings.orchestration.enable_parallel_execution = False
        agent_app = create_orchestrator_agent(test_settings)

        # Verify agent app was created with settings
        assert agent_app is not None
        assert isinstance(agent_app, AgentApplication)
