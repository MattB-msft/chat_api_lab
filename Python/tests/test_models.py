"""
Tests for data models.
"""

import pytest

from models.intent import Intent, IntentType
from models.agent_response import AgentResponse


class TestIntent:
    """Tests for Intent model."""

    def test_intent_creation(self):
        """Test creating an intent."""
        intent = Intent(
            type=IntentType.M365_EMAIL,
            query="What emails do I have?",
            confidence=0.95
        )

        assert intent.type == IntentType.M365_EMAIL
        assert intent.query == "What emails do I have?"
        assert intent.confidence == 0.95

    def test_intent_is_m365_intent_true(self):
        """Test is_m365_intent property for M365 intents."""
        m365_intents = [
            IntentType.M365_EMAIL,
            IntentType.M365_CALENDAR,
            IntentType.M365_FILES,
            IntentType.M365_PEOPLE
        ]

        for intent_type in m365_intents:
            intent = Intent(type=intent_type, query="test")
            assert intent.is_m365_intent is True

    def test_intent_is_m365_intent_false(self):
        """Test is_m365_intent property for non-M365 intents."""
        intent = Intent(
            type=IntentType.GENERAL_KNOWLEDGE,
            query="What is Docker?"
        )

        assert intent.is_m365_intent is False

    def test_intent_string_conversion(self):
        """Test creating intent from string type."""
        intent = Intent(type="M365Email", query="test")
        assert intent.type == IntentType.M365_EMAIL


class TestAgentResponse:
    """Tests for AgentResponse model."""

    def test_agent_response_creation(self):
        """Test creating an agent response."""
        response = AgentResponse(
            agent="m365_copilot",
            intent_type=IntentType.M365_EMAIL,
            content="You have 5 unread emails",
            success=True
        )

        assert response.agent == "m365_copilot"
        assert response.intent_type == IntentType.M365_EMAIL
        assert response.content == "You have 5 unread emails"
        assert response.success is True
        assert response.error is None

    def test_agent_response_with_error(self):
        """Test creating an error agent response."""
        response = AgentResponse(
            agent="m365_copilot",
            intent_type=IntentType.M365_EMAIL,
            content="Error occurred",
            success=False,
            error="Connection timeout"
        )

        assert response.success is False
        assert response.error == "Connection timeout"

    def test_agent_response_to_dict(self):
        """Test converting response to dictionary."""
        response = AgentResponse(
            agent="azure_openai",
            intent_type=IntentType.GENERAL_KNOWLEDGE,
            content="Docker is a containerization platform",
            success=True,
            metadata={"source": "azure_openai"}
        )

        response_dict = response.to_dict()

        assert response_dict["agent"] == "azure_openai"
        assert response_dict["intent_type"] == "GeneralKnowledge"
        assert response_dict["content"] == "Docker is a containerization platform"
        assert response_dict["success"] is True
        assert response_dict["metadata"]["source"] == "azure_openai"
