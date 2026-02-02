"""
Azure OpenAI plugin for general knowledge queries.

Handles queries that don't require M365 data access.
"""

from typing import Annotated
import logging

from semantic_kernel import Kernel
from semantic_kernel.functions import kernel_function

from plugins.agent_context import AgentContext


class AzureOpenAIPlugin:
    """
    Plugin for handling general knowledge queries using Azure OpenAI.

    This agent handles queries that don't require access to Microsoft 365 data.
    """

    def __init__(self, context: AgentContext, kernel: Kernel):
        """
        Initialize the Azure OpenAI plugin.

        Args:
            context: Agent context with request state and dependencies.
            kernel: Semantic Kernel instance for LLM calls.
        """
        self._context = context
        self._kernel = kernel
        self._logger = context.logger or logging.getLogger(__name__)

    @kernel_function(
        name="AnswerGeneralQuestion",
        description="Answers general knowledge questions using Azure OpenAI"
    )
    async def answer_general_question(
        self,
        question: Annotated[str, "The general knowledge question to answer"]
    ) -> Annotated[str, "The answer to the question"]:
        """
        Answer a general knowledge question using Azure OpenAI.

        Args:
            question: The question to answer.

        Returns:
            The answer from Azure OpenAI.
        """
        self._logger.info(f"Answering general question: {question}")

        prompt = f"""You are a helpful AI assistant. Answer the following question clearly and concisely.

Question: {question}

Provide a clear, accurate answer based on your knowledge."""

        result = await self._kernel.invoke_prompt(prompt=prompt)
        answer = str(result) if result else "I'm unable to answer that question at the moment."

        self._logger.info(f"Generated answer of length {len(answer)}")

        return answer
