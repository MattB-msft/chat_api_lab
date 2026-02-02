"""
Microsoft 365 Copilot Chat API plugin.

SEMANTIC KERNEL CONCEPTS:
- @kernel_function: Exposes method to the AI orchestrator
- Annotations: Helps LLM understand when to use this function
- Parameters with annotations: Helps LLM provide correct arguments

The Kernel can invoke these functions based on user intent,
allowing natural language to trigger specific API calls.

COPILOT CHAT API:
- Endpoint: /beta/copilot/conversations (Beta API - subject to change)
- Two-step process: Create conversation, then send chat message
- Returns M365 data (emails, calendar, files) in natural language
- Requires per-user Copilot license

See: https://learn.microsoft.com/graph/api/resources/copilot-api-overview

This implementation mirrors the .NET M365CopilotPlugin.cs
"""

from typing import Annotated, Optional, List
import logging

from plugins.user_authorization_token_wrapper import UserAuthorizationTokenWrapper
from semantic_kernel import Kernel
from semantic_kernel.functions import kernel_function
from microsoft_agents_m365copilot_beta import ( 
    AgentsM365CopilotBetaServiceClient 
    )
from microsoft_agents_m365copilot_beta.generated.models import (
    copilot_conversation,
)


from plugins.agent_context import AgentContext

# Required scopes for M365 Copilot access
# Mirrors the requiredScopesList in .NET (lines 38-53)
REQUIRED_SCOPES = [
    "email",
    "User.Read",
    "Mail.Read",
    "Calendars.Read",
    "Files.Read.All",
    "Sites.Read.All",
    "People.Read.All",
    "Chat.Read",
    "OnlineMeetingTranscript.Read.All",
    "ChannelMessage.Read.All",
    "ExternalItem.Read.All"
]


class M365CopilotPlugin:
    """
    Semantic Kernel Plugin for Microsoft 365 Copilot Chat API integration.

    Integrates with M365 Copilot to query enterprise data (emails, calendar, files, people).

    Uses the two-step Copilot API pattern:
    1. Create conversation (or reuse existing conversation ID)
    2. Send chat message and get response

    This implementation can be enabled in two ways:
    1. WITH M365 SDK: Uncomment imports and use real API (production)
    2. WITHOUT M365 SDK: Use mock responses for development/testing

    Mirrors: AgentOrchestrator/src/Plugins/M365CopilotPlugin.cs
    """

    def __init__(self, context: AgentContext, kernel: Kernel):
        """
        Initialize the M365 Copilot plugin.

        Args:
            context: Agent context with request state and dependencies.
            kernel: Semantic Kernel instance.
        """
        self._context = context
        self._kernel = kernel
        self._logger = context.logger or logging.getLogger(__name__)

    @kernel_function(
        name="QueryEmails",
        description="Query Microsoft 365 Copilot for email-related questions using the Chat API"
    )
    async def query_emails(
        self,
        query: Annotated[str, "The email-related question"]
    ) -> Annotated[str, "Response from M365 Copilot about emails"]:
        """
        Query M365 Copilot for email information.

        Mirrors: QueryEmailsAsync in M365CopilotPlugin.cs:69-74

        Args:
            query: Email-related question.

        Returns:
            Response from Copilot Chat API.
        """
        return await self._call_copilot_chat_api(query)

    @kernel_function(
        name="QueryCalendar",
        description="Query Microsoft 365 Copilot for calendar-related questions using the Chat API"
    )
    async def query_calendar(
        self,
        query: Annotated[str, "The calendar-related question"]
    ) -> Annotated[str, "Response from M365 Copilot about calendar"]:
        """
        Query M365 Copilot for calendar information.

        Mirrors: QueryCalendarAsync in M365CopilotPlugin.cs:78-83

        Args:
            query: Calendar-related question.

        Returns:
            Response from Copilot Chat API.
        """
        return await self._call_copilot_chat_api(query)

    @kernel_function(
        name="QueryFiles",
        description="Query Microsoft 365 Copilot for file and document questions using the Chat API"
    )
    async def query_files(
        self,
        query: Annotated[str, "The files-related question"]
    ) -> Annotated[str, "Response from M365 Copilot about files"]:
        """
        Query M365 Copilot for file and document information.

        Mirrors: QueryFilesAsync in M365CopilotPlugin.cs:87-92

        Args:
            query: Files-related question.

        Returns:
            Response from Copilot Chat API.
        """
        return await self._call_copilot_chat_api(query)

    @kernel_function(
        name="QueryPeople",
        description="Query Microsoft 365 Copilot for people and organization questions using the Chat API"
    )
    async def query_people(
        self,
        query: Annotated[str, "The people-related question"]
    ) -> Annotated[str, "Response from M365 Copilot about people"]:
        """
        Query M365 Copilot for people and organization information.

        Mirrors: QueryPeopleAsync in M365CopilotPlugin.cs:96-101

        Args:
            query: People-related question.

        Returns:
            Response from Copilot Chat API.
        """
        return await self._call_copilot_chat_api(query)

    async def _call_copilot_chat_api(self, query: str) -> str:
        """
        Call M365 Copilot Chat API with two-step pattern.

        Mirrors: CallCopilotChatApiAsync in M365CopilotPlugin.cs:103-196

        This method:
        1. Gets or creates a conversation ID
        2. Sends the user's query to the conversation
        3. Returns the Copilot response

        Args:
            query: The user's question.

        Returns:
            Response from Copilot or error message.
        """
        self._logger.info(f"Calling Copilot Chat API with query: {query}")


        try:
            # Get conversation ID from state
            conversation_id = self._context.state.conversation.get_value("M365CopilotConversationId")

            # Create Copilot client with user's access token
            client = await self._create_copilot_client()

            # Step 1: Get or create conversation ID (mirrors lines 117-132)
            if not conversation_id:
                self._logger.info("Creating conversation...")

                b : copilot_conversation = copilot_conversation.CopilotConversation()
                # POST to /beta/copilot/conversations
                conversation = await client.copilot.conversations.post(
                    body=b
                )

                if not conversation or not conversation.id:
                    raise RuntimeError("Failed to create conversation - no ID returned")

                conversation_id = conversation.id
                self._context.state.conversation.set_value("M365CopilotConversationId", conversation_id)
                self._logger.info(f"Created conversation: {conversation_id}")

            # Step 2: Send chat message (mirrors lines 134-159)
            self._logger.info("Sending chat message...")

            # Import chat-specific types
            # Note: The exact import path may vary based on SDK version
            try:
                # Try to import ChatPostRequestBody
                # The structure mirrors the .NET ChatPostRequestBody (lines 136-143)
                chat_request_body = {
                    "message": {
                        "text": query
                    },
                    "locationHint": {
                        "timeZone": "America/Los_Angeles"
                    }
                }

                # POST to /beta/copilot/conversations/{id}/microsoft.graph.copilot.chat
                # This endpoint mirrors line 146 in .NET
                response = await client.copilot.conversations.by_copilot_conversation_id(
                    conversation_id
                ).microsoft_graph_copilot_chat.post(chat_request_body)

                # Extract response (mirrors lines 148-159)
                if not response or not hasattr(response, 'messages') or not response.messages:
                    self._logger.warning("No messages in response")
                    return "No response received from Copilot."

                # Get the assistant's response (last message)
                assistant_message = response.messages[-1]
                response_text = getattr(assistant_message, 'text', None) or "No response content."

                self._logger.info("Received Copilot response")
                return response_text

            except AttributeError as e:
                # Handle case where SDK structure is different than expected
                self._logger.error(f"SDK structure mismatch: {e}")
                return self._get_fallback_response(query, conversation_id, client)

        except Exception as ex:
            # Mirrors lines 181-185 in .NET
            self._logger.error(f"Error calling Copilot Chat API: {ex}", exc_info=True)

            # Check for specific error types
            error_msg = str(ex).lower()

            if "401" in error_msg or "unauthorized" in error_msg:
                return "Your session has expired. Please log in again."
            elif "403" in error_msg or "forbidden" in error_msg:
                return "You don't have access to Microsoft 365 Copilot. Please contact your administrator to verify your license."
            elif "404" in error_msg or "not found" in error_msg:
                return "The Copilot service is not available. Please try again later."
            elif "500" in error_msg or "server error" in error_msg:
                return "The Copilot service encountered an error. Please try again later."
            else:
                # Re-raise for unexpected errors
                raise

    async def _create_copilot_client(self):
        """
        Create M365 Copilot API client with authentication.

        Mirrors: CreateCopilotClientAsync in M365CopilotPlugin.cs:198-214

        This creates an authenticated client using the user's access token.

        Returns:
            AgentsM365CopilotBetaServiceClient instance.

        Raises:
            ImportError: If SDK is not installed.
            RuntimeError: If access token is not available.
        """

        wrapper = UserAuthorizationTokenWrapper( 
            user_authorization=self._context.user_auth,
            turn_context=self._context.context,
            handler_name=self._context.auth_handler_name
        )


        # Get user's access token
        # Mirrors: await _context.UserAuth.ExchangeTurnTokenAsync() (lines 204-207)
        access_token = await self._context.user_auth.exchange_token(
            context=self._context.context,
            exchange_connection=self._context.connection_name,
            scopes=REQUIRED_SCOPES, 
            auth_handler_id=self._context.auth_handler_name)

        if not access_token:
            raise RuntimeError(
                "Access token not available. Ensure authentication is configured."
            )

        # Create client 
        # Python SDK automatically handles request adapter creation
        client = AgentsM365CopilotBetaServiceClient(credentials=wrapper)

        return client

    async def _get_fallback_response(
        self,
        query: str,
        conversation_id: str,
        client
    ) -> str:
        """
        Fallback method for SDK structure variations.

        Attempts alternative API call patterns if the primary method fails.

        Args:
            query: User's query.
            conversation_id: Conversation ID.
            client: M365 Copilot client.

        Returns:
            Response from Copilot or error message.
        """
        try:
            # Try alternative access pattern
            self._logger.info("Attempting fallback API access pattern...")

            # Alternative: Direct REST-like call
            # This may work if the SDK has different accessor methods
            conversations = client.copilot.conversations
            conversation = conversations.by_id(conversation_id)

            # Try different chat endpoint access
            chat_response = await conversation.chat.post({
                "message": {"text": query}
            })

            if chat_response and hasattr(chat_response, 'messages'):
                messages = chat_response.messages
                if messages:
                    return messages[-1].text or "No response content."

            return "Unable to retrieve response from Copilot."

        except Exception as e:
            self._logger.error(f"Fallback method also failed: {e}")
            return f"Error communicating with M365 Copilot: {str(e)}"

    def _get_mock_response(self, query: str) -> str:
        """
        Get mock response when SDK is not available.

        Args:
            query: User's query.

        Returns:
            Mock response indicating SDK is not installed.
        """
        return f"""[MOCK M365 COPILOT RESPONSE - SDK NOT INSTALLED]

Query: "{query}"

To enable real M365 Copilot integration:

1. Install the SDK:
   pip install microsoft-agents-m365copilot-beta

2. Configure authentication:
   - Set up access token in AgentContext
   - Ensure user has M365 Copilot license
   - Configure required scopes:
     {', '.join(REQUIRED_SCOPES[:5])}...

3. Test with real M365 data

For development, this mock response allows testing the orchestration
flow without requiring actual M365 Copilot access.
"""


# SECURITY: Token provider helper class
# Mirrors TokenProvider class in M365CopilotPlugin.cs:217-245
class TokenProvider:
    """
    Token provider that restricts which hosts receive the access token.

    Why this matters:
    - Access tokens should only be sent to intended APIs
    - If a redirect or misconfiguration sends requests elsewhere, token won't leak
    - AllowedHostsValidator is a defense-in-depth measure

    Only Microsoft Graph hosts are allowed to receive this token.
    """

    ALLOWED_HOSTS = ["graph.microsoft.com", "graph.microsoft-ppe.com"]

    def __init__(self, access_token: str):
        """
        Initialize token provider with access token.

        Args:
            access_token: The access token to provide.
        """
        self._access_token = access_token

    def is_host_allowed(self, url: str) -> bool:
        """
        Check if a URL's host is allowed to receive the token.

        Args:
            url: URL to check.

        Returns:
            True if host is allowed, False otherwise.
        """
        from urllib.parse import urlparse

        parsed = urlparse(url)
        return parsed.hostname in self.ALLOWED_HOSTS

    async def get_token(self, url: str) -> Optional[str]:
        """
        Get access token if the URL's host is allowed.

        Args:
            url: URL requesting the token.

        Returns:
            Access token if host is allowed, None otherwise.
        """
        if self.is_host_allowed(url):
            return self._access_token
        return None
