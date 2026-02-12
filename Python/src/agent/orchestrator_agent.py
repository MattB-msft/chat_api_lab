"""
Orchestrator agent using M365 Agents SDK AgentApplication pattern.

This follows the same pattern as the .NET OrchestratorAgent.cs which extends
AgentApplication and uses OnActivity handlers for message routing.
"""

import asyncio
import json
import uuid
from typing import List, Optional
import logging

from microsoft_agents.hosting.core import (
    Authorization,
    TurnContext,
    MessageFactory,
    MemoryStorage,
    AgentApplication,
    TurnState,
    MemoryStorage,
)
from microsoft_agents.activity import load_configuration_from_env, ActivityTypes
from microsoft_agents.authentication.msal import MsalConnectionManager
from microsoft_agents.hosting.fastapi import CloudAdapter

from semantic_kernel import Kernel
from semantic_kernel.connectors.ai.open_ai import AzureChatCompletion

from models.intent import Intent, IntentType
from models.agent_response import AgentResponse
from models.configuration import AppSettings
from constants.plugin_names import PluginNames
from plugins.agent_context import AgentContext
from plugins.intent_plugin import IntentPlugin
from plugins.m365_copilot_plugin import M365CopilotPlugin
from plugins.azure_openai_plugin import AzureOpenAIPlugin
from plugins.synthesis_plugin import SynthesisPlugin


# Constants matching .NET implementation
MAX_MESSAGE_LENGTH = 4000
AGENTIC_AUTH_HANDLER = "agentic"
NON_AGENTIC_AUTH_HANDLER = "me"

class OrchestratorAgentApp:
    """
    Orchestrator agent application following M365 Agents SDK pattern.

    This class mirrors the .NET OrchestratorAgent which extends AgentApplication.
    It initializes the agent application, registers handlers, and manages the
    3-step orchestration flow: Intent Analysis → Agent Execution → Response Synthesis.
    """
    

    def __init__(self, settings: AppSettings, connection_manager: MsalConnectionManager):
        """
        Initialize the orchestrator agent application.

        Args:
            settings: Application configuration.
            connection_manager: MsalConnectionManager instance.
        """
        self._settings = settings
        self._logger = logging.getLogger(__name__)

        self._local_connection_manager: MsalConnectionManager = connection_manager

        # Initialize Semantic Kernel with Azure OpenAI
        self._base_kernel = self._create_kernel()

        # Initialize M365 Agents SDK AgentApplication
        self._agent_app = AgentApplication[TurnState](
            storage=MemoryStorage(),
            adapter=CloudAdapter(connection_manager=connection_manager),
            authorization=Authorization(MemoryStorage(),
                            connection_manager= connection_manager, 
                            **settings.agents_sdk_config)
        )

        # Register activity handlers
        self._register_handlers()

        self._logger.info("OrchestratorAgent initialized with M365 Agents SDK pattern")

    @property
    def agent_app(self) -> AgentApplication:
        """Get the underlying AgentApplication instance."""
        return self._agent_app

    def _register_handlers(self):
        """
        Register activity handlers with the agent application.
        """

        # Handle conversation updates (e.g., user joins)
        @self._agent_app.conversation_update("membersAdded")
        async def on_conversation_update(context: TurnContext, state: TurnState):
            """Handle conversation updates when members are added."""
            await context.send_activity(
                "Welcome! I'm a multi-agent orchestrator that can help with "
                "Microsoft 365 data (emails, calendar, files, people) and general knowledge. "
                "Ask me anything!"
            )

        # Handle message activities (user messages)
        # This is the main handler that mirrors OnMessageActivityAsync in .NET (line 47-136)
        @self._agent_app.activity("message", auth_handlers=["GRAPH"])
        async def on_message_activity(context: TurnContext, state: TurnState):
            """
            Handle incoming message activities with 3-step orchestration flow.

            This is the main orchestration handler that mirrors the .NET
            OnMessageActivityAsync method in OrchestratorAgent.cs:47-136.

            Args:
                context: Turn context from M365 Agents SDK.
                state: Turn state for conversation management.
            """
            user_message = context.activity.text.strip() if context.activity.text else ""

            # Input validation (mirrors lines 58-73 in .NET)
            if not user_message:
                await context.send_activity("Please enter a message.")
                return

            if len(user_message) > MAX_MESSAGE_LENGTH:
                await context.send_activity(
                    f"Message too long. Maximum {MAX_MESSAGE_LENGTH} characters allowed."
                )
                return

            self._logger.info(f"Processing message: {user_message}")

            try:
                # Create timeout-aware cancellation (mirrors lines 80-82 in .NET)
                async with asyncio.timeout(self._settings.orchestration.timeout_seconds):
                    # Generate request ID for tracking
                    request_id = str(uuid.uuid4())

                    # Get access token if available
                    # TODO: Implement token acquisition via self._agent_app.auth.get_token()
                    # Mirrors: await _context.UserAuth.ExchangeTurnTokenAsync()
                    access_token = None

                    # Create agent context for this turn
                    agent_context = AgentContext(
                        context=context,
                        state=state,
                        connection_name= 'SERVICE_CONNECTION',
                        user_auth=self._agent_app.auth,
                        auth_handler_name='GRAPH',  #context.authorization.get_active_handler_name(),
                        logger=self._logger
                    )

                    # Setup kernel with plugins for this turn (mirrors line 85)
                    kernel = self._setup_plugins(agent_context)

                    # Step 1: Analyze intent (mirrors lines 87-89)
                    self._logger.info("Step 1: Analyzing intent...")
                    intents = await self._analyze_intent(kernel, user_message)

                    # Apply MaxAgentCalls limit (mirrors lines 92-97)
                    if len(intents) > self._settings.orchestration.max_agent_calls:
                        self._logger.warning(
                            f"Truncating intents from {len(intents)} to "
                            f"{self._settings.orchestration.max_agent_calls}"
                        )
                        intents = intents[:self._settings.orchestration.max_agent_calls]

                    self._logger.info(
                        f"Detected {len(intents)} intent(s): "
                        f"{', '.join(i.type.value for i in intents)}"
                    )

                    # Step 2: Execute agents based on intents (mirrors lines 103-106)
                    self._logger.info(
                        f"Step 2: Executing agents "
                        f"(parallel={self._settings.orchestration.enable_parallel_execution})..."
                    )
                    responses = await self._execute_agents(kernel, intents)

                    # Step 3: Synthesize response (mirrors lines 108-110)
                    self._logger.info("Step 3: Synthesizing response...")
                    final_response = await self._synthesize_response(
                        kernel, user_message, responses
                    )

                    # Step 4: Send response (mirrors line 113)
                    await context.send_activity(final_response)

                    self._logger.info("Response sent successfully")

            except asyncio.TimeoutError:
                # Mirrors lines 121-125 in .NET
                self._logger.warning(
                    f"Request timed out after {self._settings.orchestration.timeout_seconds} seconds"
                )
                await context.send_activity(
                    "The request timed out. Please try a simpler query or try again later."
                )
            except Exception as ex:
                # Mirrors lines 126-131 in .NET
                self._logger.error(f"Error processing message: {ex}", exc_info=True)
                # Don't expose internal error details to users
                await context.send_activity(
                    "Sorry, an error occurred processing your request. Please try again."
                )

        # Register help command
        @self._agent_app.message("/help")
        async def on_help(context: TurnContext, state: TurnState):
            """Handle help command."""
            help_text = """
**Available Commands:**
- Ask questions about your M365 data (emails, calendar, files, people)
- Ask general knowledge questions
- /help - Show this help message

**Example Queries:**
- "What meetings do I have tomorrow?"
- "Summarize my recent emails"
- "What is Docker?"
- "What meetings do I have and what is Kubernetes?" (multi-intent)
"""
            await context.send_activity(help_text)

    def _create_kernel(self) -> Kernel:
        """
        Create base Semantic Kernel instance with Azure OpenAI.

        Returns:
            Configured Kernel instance.
        """
        kernel = Kernel()

        # Add Azure OpenAI chat completion service
        service_id = "azure_openai"
        kernel.add_service(
            AzureChatCompletion(
                service_id=service_id,
                deployment_name=self._settings.azure_openai.deployment_name,
                endpoint=self._settings.azure_openai.endpoint,
                api_key=self._settings.azure_openai.api_key,
                api_version=self._settings.azure_openai.api_version
            )
        )

        return kernel

    def _setup_plugins(self, context: AgentContext) -> Kernel:
        """
        Create per-turn kernel with plugins registered with fresh context.

        This mirrors UpdateChatClientWithToolsInContext in .NET (line 361).
        Plugins are registered per-turn to ensure each has access to current
        turn's context and state.

        Args:
            context: Agent context for this turn.

        Returns:
            Kernel with plugins registered.
        """
        # Clone base kernel for this turn
        kernel = Kernel()

        # Copy service from base kernel
        for service_id, service in self._base_kernel.services.items():
            kernel.services[service_id] = service

        # Register plugins with current context
        # Mirrors: _kernel.Plugins.AddFromObject(new IntentPlugin(agentContext, ...))
        intent_plugin = IntentPlugin(context, kernel)
        kernel.add_plugin(intent_plugin, PluginNames.INTENT)

        m365_plugin = M365CopilotPlugin(context, kernel)
        kernel.add_plugin(m365_plugin, PluginNames.M365_COPILOT)

        openai_plugin = AzureOpenAIPlugin(context, kernel)
        kernel.add_plugin(openai_plugin, PluginNames.AZURE_OPENAI)

        synthesis_plugin = SynthesisPlugin(context, kernel)
        kernel.add_plugin(synthesis_plugin, PluginNames.SYNTHESIS)

        return kernel

    async def _analyze_intent(self, kernel: Kernel, query: str) -> List[Intent]:
        """
        Analyze user query to determine intents.

        Mirrors AnalyzeIntentAsync in OrchestratorAgent.cs:138-169.

        Args:
            kernel: Kernel with plugins registered.
            query: User query to analyze.

        Returns:
            List of detected intents with fallback to GeneralKnowledge.
        """
        try:
            # Invoke intent plugin (mirrors lines 140-146)
            result = await kernel.invoke(
                plugin_name=PluginNames.INTENT,
                function_name="AnalyzeIntent",
                query=query
            )

            json_response = str(result) if result else "[]"

            data = json.loads(json_response)
            intents = [Intent(**item) for item in data]

            return intents

        except Exception as e:
            self._logger.error(f"Error analyzing intent: {e}", exc_info=True)
            # Fallback to general knowledge on any error
            return [Intent(type=IntentType.GENERAL_KNOWLEDGE, query=query)]

    async def _execute_agents(
        self,
        kernel: Kernel,
        intents: List[Intent]
    ) -> List[AgentResponse]:
        """
        Execute agents based on detected intents.

        Mirrors ExecuteAgentsAsync in OrchestratorAgent.cs:172-193.

        Args:
            kernel: Kernel with plugins registered.
            intents: List of intents to process.

        Returns:
            List of agent responses.
        """
        if self._settings.orchestration.enable_parallel_execution:
            # Parallel execution with Task.WhenAll equivalent (lines 176-180)
            tasks = [
                self._execute_agent_for_intent(kernel, intent)
                for intent in intents
            ]
            responses = await asyncio.gather(*tasks, return_exceptions=True)

            # Convert exceptions to error responses
            result = []
            for i, response in enumerate(responses):
                if isinstance(response, Exception):
                    self._logger.error(
                        f"Error in parallel execution for intent {intents[i].type}: {response}"
                    )
                    result.append(AgentResponse(
                        agent=intents[i].type.value,
                        intent_type=intents[i].type,
                        content=f"Error: {str(response)}",
                        success=False
                    ))
                else:
                    result.append(response)

            return result
        else:
            # Sequential execution (lines 184-192)
            responses = []
            for intent in intents:
                response = await self._execute_agent_for_intent(kernel, intent)
                responses.append(response)
            return responses

    async def _execute_agent_for_intent(
        self,
        kernel: Kernel,
        intent: Intent
    ) -> AgentResponse:
        """
        Execute appropriate agent for a specific intent.

        Mirrors ExecuteAgentForIntentAsync in OrchestratorAgent.cs:195-228.

        Args:
            kernel: Kernel with plugins registered.
            intent: Intent to process.

        Returns:
            Agent response.
        """
        try:
            # Switch on intent type (mirrors lines 201-215)
            if intent.type == IntentType.M365_EMAIL:
                return await self._execute_m365_plugin(
                    kernel, "QueryEmails", intent.query, IntentType.M365_EMAIL
                )
            elif intent.type == IntentType.M365_CALENDAR:
                return await self._execute_m365_plugin(
                    kernel, "QueryCalendar", intent.query, IntentType.M365_CALENDAR
                )
            elif intent.type == IntentType.M365_FILES:
                return await self._execute_m365_plugin(
                    kernel, "QueryFiles", intent.query, IntentType.M365_FILES
                )
            elif intent.type == IntentType.M365_PEOPLE:
                return await self._execute_m365_plugin(
                    kernel, "QueryPeople", intent.query, IntentType.M365_PEOPLE
                )
            elif intent.type == IntentType.GENERAL_KNOWLEDGE:
                return await self._execute_general_knowledge(kernel, intent.query)
            else:
                return AgentResponse(
                    agent="unknown",
                    intent_type=intent.type,
                    content="I'm not sure how to handle that request.",
                    success=False
                )

        except Exception as e:
            # Mirrors lines 217-227
            self._logger.error(f"Error executing agent for intent {intent.type}: {e}")
            return AgentResponse(
                agent=intent.type.value,
                intent_type=intent.type,
                content=f"Error: {str(e)}",
                success=False,
                error=str(e)
            )

    async def _execute_m365_plugin(
        self,
        kernel: Kernel,
        function_name: str,
        query: str,
        intent_type: IntentType
    ) -> AgentResponse:
        """
        Execute M365 Copilot plugin function.

        Mirrors ExecuteM365PluginAsync in OrchestratorAgent.cs:230-265.

        Args:
            kernel: Kernel with plugins registered.
            function_name: Plugin function to invoke.
            query: Query to pass to function.
            intent_type: Type of intent being processed.

        Returns:
            Agent response.
        """
        result = await kernel.invoke(
            plugin_name=PluginNames.M365_COPILOT,
            function_name=function_name,
            query=query
        )

        content = str(result) if result else "No response received."

        return AgentResponse(
            agent="m365_copilot",
            intent_type=intent_type,
            content=content,
            success=True
        )

    async def _execute_general_knowledge(
        self,
        kernel: Kernel,
        query: str
    ) -> AgentResponse:
        """
        Execute Azure OpenAI plugin for general knowledge.

        Mirrors ExecuteGeneralKnowledgeAsync in OrchestratorAgent.cs:267-283.

        Args:
            kernel: Kernel with plugins registered.
            query: Query to answer.

        Returns:
            Agent response.
        """
        result = await kernel.invoke(
            plugin_name=PluginNames.AZURE_OPENAI,
            function_name="AnswerGeneralQuestion",
            question=query
        )

        content = str(result) if result else "I'm unable to answer that question."

        return AgentResponse(
            agent="azure_openai",
            intent_type=IntentType.GENERAL_KNOWLEDGE,
            content=content,
            success=True
        )

    async def _synthesize_response(
        self,
        kernel: Kernel,
        original_query: str,
        responses: List[AgentResponse]
    ) -> str:
        """
        Synthesize multiple agent responses into coherent answer.

        Mirrors SynthesizeResponseAsync in OrchestratorAgent.cs:285-306.

        Args:
            kernel: Kernel with plugins registered.
            original_query: Original user query.
            responses: List of agent responses to synthesize.

        Returns:
            Synthesized response.
        """
        # Format responses for synthesis
        synthesis_plugin = kernel.plugins[PluginNames.SYNTHESIS]
        formatted_responses = SynthesisPlugin.format_responses_for_synthesis(responses)

        # Invoke synthesis
        result = await kernel.invoke(
            plugin_name=PluginNames.SYNTHESIS,
            function_name="Synthesize",
            original_query=original_query,
            responses=formatted_responses
        )

        return str(result) if result else "I couldn't generate a response."


# Factory function to create and configure the orchestrator
def create_orchestrator_agent(settings: AppSettings, connection_manager: MsalConnectionManager) -> AgentApplication[TurnState]:
    """
    Create and configure the orchestrator agent application.

    This is the main entry point that should be used in main.py.

    Args:
        settings: Application configuration.
        connection_manager: MsalConnectionManager instance.

    Returns:
        Configured AgentApplication instance ready for use with CloudAdapter.
    """
    orchestrator = OrchestratorAgentApp(settings=settings, connection_manager=connection_manager)
    return orchestrator.agent_app
