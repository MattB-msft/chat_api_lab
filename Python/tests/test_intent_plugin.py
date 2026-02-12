"""
Tests for IntentPlugin.
"""

import pytest
import json

from plugins.intent_plugin import IntentPlugin
from models.intent import Intent, IntentType


class TestIntentPlugin:
    """Tests for IntentPlugin."""

    def test_extract_json_with_markdown(self):
        """Test JSON extraction from markdown code blocks."""
        response_with_markdown = '''```json
[
  {"type": "M365Email", "query": "What emails do I have?"}
]
```'''

        extracted = IntentPlugin._extract_json(response_with_markdown)
        # Should extract just the JSON content, stripped
        assert '{"type": "M365Email"' in extracted
        assert '"query": "What emails do I have?"' in extracted

    def test_extract_json_without_json_tag(self):
        """Test JSON extraction from generic code blocks."""
        response_with_code_block = '''```
[
  {"type": "M365Calendar", "query": "What meetings?"}
]
```'''

        extracted = IntentPlugin._extract_json(response_with_code_block)
        # Should extract just the JSON content, stripped
        assert '{"type": "M365Calendar"' in extracted
        assert '"query": "What meetings?"' in extracted

    def test_extract_json_plain(self):
        """Test JSON extraction from plain text."""
        plain_json = '[{"type": "GeneralKnowledge", "query": "What is Docker?"}]'

        extracted = IntentPlugin._extract_json(plain_json)
        assert extracted == plain_json

    def test_parse_intent_response_success(self, mock_context, mock_kernel):
        """Test successful intent parsing."""
        plugin = IntentPlugin(mock_context, mock_kernel)

        json_response = json.dumps([
            {"type": "M365Email", "query": "Check emails"},
            {"type": "GeneralKnowledge", "query": "Explain Docker"}
        ])

        intents = plugin.parse_intent_response(json_response, "original query")

        assert len(intents) == 2
        assert intents[0].type == IntentType.M365_EMAIL
        assert intents[0].query == "Check emails"
        assert intents[1].type == IntentType.GENERAL_KNOWLEDGE
        assert intents[1].query == "Explain Docker"

    def test_parse_intent_response_empty_list(self, mock_context, mock_kernel):
        """Test fallback when response is empty list."""
        plugin = IntentPlugin(mock_context, mock_kernel)

        json_response = "[]"
        intents = plugin.parse_intent_response(json_response, "original query")

        assert len(intents) == 1
        assert intents[0].type == IntentType.GENERAL_KNOWLEDGE
        assert intents[0].query == "original query"

    def test_parse_intent_response_invalid_json(self, mock_context, mock_kernel):
        """Test fallback when JSON is invalid."""
        plugin = IntentPlugin(mock_context, mock_kernel)

        invalid_json = "this is not json"
        intents = plugin.parse_intent_response(invalid_json, "original query")

        assert len(intents) == 1
        assert intents[0].type == IntentType.GENERAL_KNOWLEDGE
        assert intents[0].query == "original query"

    def test_parse_intent_response_invalid_intent_type(self, mock_context, mock_kernel):
        """Test handling of invalid intent types."""
        plugin = IntentPlugin(mock_context, mock_kernel)

        json_response = json.dumps([
            {"type": "InvalidType", "query": "test"}
        ])

        intents = plugin.parse_intent_response(json_response, "original query")

        # Should fallback to GeneralKnowledge when no valid intents
        assert len(intents) == 1
        assert intents[0].type == IntentType.GENERAL_KNOWLEDGE
