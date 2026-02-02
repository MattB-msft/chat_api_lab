"""
Response synthesis plugin for combining multiple agent responses.

Takes responses from multiple agents and synthesizes them into a coherent answer.
"""

from typing import Annotated, List
import json
import logging

from semantic_kernel import Kernel
from semantic_kernel.functions import kernel_function

from models.agent_response import AgentResponse
from plugins.agent_context import AgentContext


class SynthesisPlugin:
    """
    Plugin for synthesizing multiple agent responses into a unified answer.

    This plugin combines responses from different agents (M365 Copilot, Azure OpenAI)
    into a single coherent response that addresses the user's query.
    """

    def __init__(self, context: AgentContext, kernel: Kernel):
        """
        Initialize the synthesis plugin.

        Args:
            context: Agent context with request state and dependencies.
            kernel: Semantic Kernel instance for LLM calls.
        """
        self._context = context
        self._kernel = kernel
        self._logger = context.logger or logging.getLogger(__name__)

    @kernel_function(
        name="Synthesize",
        description="Synthesizes multiple agent responses into a coherent unified response"
    )
    async def synthesize(
        self,
        original_query: Annotated[str, "The original user query"],
        responses: Annotated[str, "JSON array of agent responses to synthesize"]
    ) -> Annotated[str, "The synthesized response"]:
        """
        Synthesize multiple agent responses into a coherent answer.

        Args:
            original_query: The user's original query.
            responses: JSON string containing array of agent responses.

        Returns:
            Synthesized response combining all agent outputs.
        """
        self._logger.info(f"Synthesizing responses for query: {original_query}")

        prompt = f"""You are a response synthesizer. Your job is to combine multiple agent responses into a single,
coherent response that addresses the user's original query.

Original User Query: {original_query}

Agent Responses:
{responses}

Instructions:
1. Analyze all the agent responses
2. Combine them into a single, well-organized response
3. Maintain clear structure - if there are multiple topics, organize them with headers or clear transitions
4. Remove any redundancy between responses
5. Ensure the response directly addresses the user's original query
6. Keep the tone helpful and conversational
7. If one response is about M365 data (emails, calendar, etc.) and another is general knowledge,
   present the M365 data first, then the general information

Synthesized Response:
"""

        result = await self._kernel.invoke_prompt(prompt=prompt)
        synthesized = str(result) if result else "I couldn't synthesize a response."

        self._logger.info(f"Synthesized response of length {len(synthesized)}")

        return synthesized

    @staticmethod
    def format_responses_for_synthesis(responses: List[AgentResponse]) -> str:
        """
        Format agent responses as JSON for synthesis.

        Args:
            responses: List of agent responses to format.

        Returns:
            JSON string representation of responses.
        """
        # Filter out failed responses
        successful_responses = [r for r in responses if r.success]

        # Convert to dictionaries
        response_dicts = [
            {
                "agent": r.agent,
                "intent_type": r.intent_type.value,
                "content": r.content
            }
            for r in successful_responses
        ]

        return json.dumps(response_dicts, indent=2)
