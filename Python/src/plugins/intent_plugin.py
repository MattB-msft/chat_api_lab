"""
Intent classification plugin for multi-agent orchestration.

Uses Azure OpenAI to analyze user queries and determine which agents should handle them.
"""

import json
import re
from typing import List, Annotated
import logging

from semantic_kernel import Kernel
from semantic_kernel.functions import kernel_function

from models.intent import Intent, IntentType
from plugins.agent_context import AgentContext


class IntentPlugin:
    """
    Plugin for analyzing user intent and routing to appropriate agents.

    Uses Azure OpenAI to classify queries into one or more intent types.
    """

    def __init__(self, context: AgentContext, kernel: Kernel):
        """
        Initialize the intent plugin.

        Args:
            context: Agent context with request state and dependencies.
            kernel: Semantic Kernel instance for LLM calls.
        """
        self._context = context
        self._kernel = kernel
        self._logger = context.logger or logging.getLogger(__name__)

    @kernel_function(
        name="AnalyzeIntent",
        description="Analyzes user intent and determines which agents should handle the query"
    )
    async def analyze_intent(
        self,
        query: Annotated[str, "The user query to analyze"]
    ) -> Annotated[str, "JSON array of intents"]:
        """
        Analyze user query and classify into one or more intents.

        Args:
            query: The user's query to analyze.

        Returns:
            JSON string containing array of intent objects.
        """
        self._logger.info(f"Analyzing intent for query: {query}")

        prompt = f"""You are an intent classifier for a multi-agent system. Analyze the user's query and identify which agents should handle it.

Available intent types:
- M365Email: Questions about emails, messages, inbox, mail
- M365Calendar: Questions about meetings, schedule, calendar, appointments
- M365Files: Questions about documents, files, SharePoint, OneDrive
- M365People: Questions about colleagues, organization, team members, expertise
- GeneralKnowledge: General questions not related to Microsoft 365 data

Rules:
1. A query can have multiple intents (e.g., "Summarize my emails and explain REST APIs" has M365Email + GeneralKnowledge)
2. If the query mentions personal data (my emails, my calendar, my files, my team), route to the appropriate M365 intent
3. If the query is about general concepts, technology, or information not in M365, use GeneralKnowledge
4. Extract the relevant sub-query for each intent

User Query: {query}

Respond with ONLY a JSON array, no other text:
[
  {{"type": "IntentType", "query": "extracted sub-query for this intent"}}
]

Example for "What meetings do I have tomorrow and what is Docker?":
[
  {{"type": "M365Calendar", "query": "What meetings do I have tomorrow"}},
  {{"type": "GeneralKnowledge", "query": "What is Docker"}}
]
"""

        # Invoke kernel with prompt
        result = await self._kernel.invoke_prompt(prompt=prompt)
        response = str(result) if result else "[]"

        # Clean up response - extract JSON if wrapped in markdown
        response = self._extract_json(response)

        return response

    def parse_intent_response(self, json_response: str, original_query: str) -> List[Intent]:
        """
        Parse JSON response into Intent objects with fallback handling.

        Args:
            json_response: JSON string from AnalyzeIntent.
            original_query: Original user query for fallback.

        Returns:
            List of Intent objects. Returns GeneralKnowledge intent on parse error.
        """
        try:
            intents_data = json.loads(json_response)

            if not isinstance(intents_data, list):
                self._logger.warning("Intent response is not a list. Defaulting to GeneralKnowledge.")
                return [Intent(type=IntentType.GENERAL_KNOWLEDGE, query=original_query)]

            if not intents_data:
                self._logger.warning("Intent response is empty. Defaulting to GeneralKnowledge.")
                return [Intent(type=IntentType.GENERAL_KNOWLEDGE, query=original_query)]

            intents = []
            for item in intents_data:
                try:
                    intent = Intent(
                        type=IntentType(item["type"]),
                        query=item["query"],
                        confidence=item.get("confidence")
                    )
                    intents.append(intent)
                except (KeyError, ValueError) as e:
                    self._logger.warning(f"Invalid intent item {item}: {e}")
                    continue

            if not intents:
                self._logger.warning("No valid intents parsed. Defaulting to GeneralKnowledge.")
                return [Intent(type=IntentType.GENERAL_KNOWLEDGE, query=original_query)]

            return intents

        except json.JSONDecodeError as e:
            self._logger.warning(f"Failed to parse intent JSON: {e}. Defaulting to GeneralKnowledge.")
            return [Intent(type=IntentType.GENERAL_KNOWLEDGE, query=original_query)]

    @staticmethod
    def _extract_json(response: str) -> str:
        """
        Extract JSON from response, handling markdown code blocks.

        Args:
            response: Raw response string that may contain markdown.

        Returns:
            Cleaned JSON string.
        """
        # Remove markdown code blocks if present
        if "```json" in response:
            match = re.search(r"```json\s*(.*?)\s*```", response, re.DOTALL)
            if match:
                return match.group(1).strip()
        elif "```" in response:
            match = re.search(r"```\s*(.*?)\s*```", response, re.DOTALL)
            if match:
                return match.group(1).strip()

        return response.strip()
