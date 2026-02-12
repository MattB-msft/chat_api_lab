"""
User Authorization Token Wrapper for Azure AI Foundry Agent authentication.

This class wraps the Authorization to provide a TokenCredential implementation
as the AI Foundry agent expects a TokenCredential to be used for authentication.

Note: To authenticate with the AI Foundry agent, the application that was used
to create the user JWT token must have the 'Azure Machine Learning Services' =>
'user_impersonation' scope configured in the Azure portal.

Mirrors: AzureAgentToM365ATK/UserAuthorizationTokenWrapper.cs
"""

from typing import Optional
import jwt

from azure.core.credentials import AccessToken
from microsoft_agents.hosting.core import TurnContext, Authorization


class UserAuthorizationTokenWrapper:
    """
    Token credential wrapper that exchanges user turn tokens for JWT tokens.

    This class bridges the gap between the Microsoft Agents SDK's Authorization
    system and Azure SDK's TokenCredential pattern. It enables using user
    authentication from the bot framework with Azure services that expect
    TokenCredential objects.

    This implementation uses duck typing (no inheritance) to match the pattern
    used in the existing M365CopilotPlugin (lines 305-315).

    Key features:
    - Automatically converts ".default" scopes to "user_impersonation"
    - Exchanges turn tokens using the Authorization service
    - Extracts JWT expiration for proper token caching
    - Compatible with Azure SDK clients (Azure OpenAI, AI Foundry, etc.)

    Example:
        ```python
        credential = UserAuthorizationTokenWrapper(
            user_authorization=agent_context.user_auth,
            turn_context=agent_context.context,
            handler_name="agentic"
        )

        # Use with Azure SDK clients (e.g., AI Foundry agent)
        client = AzureAIClient(
            endpoint="https://...",
            credential=credential
        )
        ```

    Mirrors: UserAuthorizationTokenWrapper.cs from davrous/AzureAgentToM365
    """

    def __init__(
        self,
        user_authorization: Authorization,
        turn_context: TurnContext,
        handler_name: str
    ):
        """
        Initialize the token wrapper.

        Args:
            user_authorization: The Authorization instance from the agent framework.
            turn_context: The current turn context containing the user's request.
            handler_name: Name of the auth handler to use (e.g., "agentic", "me").

        Raises:
            ValueError: If handler_name or turn_context is None.

        Mirrors: Constructor in UserAuthorizationTokenWrapper.cs:25-30
        """
        if not handler_name:
            raise ValueError("handler_name cannot be None or empty")
        if not turn_context:
            raise ValueError("turn_context cannot be None")

        self._user_authorization = user_authorization
        self._handler_name = handler_name
        self._turn_context = turn_context

    async def get_token(
        self,
        *scopes: str,
        **kwargs
    ) -> AccessToken:
        """
        Get an access token for the specified scopes.

        This method exchanges the current user's turn token for a JWT token
        that can be used to authenticate with Azure services like AI Foundry.

        The method automatically handles scope translation:
        - "*.default" scopes are converted to "*user_impersonation" scopes
        - This is required for Azure Machine Learning/AI Foundry authentication

        Args:
            *scopes: The scopes to request (e.g., "https://ml.azure.com/.default").
            **kwargs: Additional keyword arguments (not used, for compatibility).

        Returns:
            AccessToken: A namedtuple with `token` (str) and `expires_on` (int)
                containing the JWT token string and Unix timestamp expiration.

        Raises:
            RuntimeError: If token exchange fails or JWT is invalid.

        Example:
            ```python
            token = await credential.get_token(
                "https://ml.azure.com/.default"
            )
            print(f"Token: {token.token[:20]}...")
            print(f"Expires: {token.expires_on}")
            ```

        Mirrors: GetTokenAsync in UserAuthorizationTokenWrapper.cs:45-51
        """
        # Step 1: Convert .default scopes to user_impersonation scopes
        # Mirrors: Lines 31-37 in C# GetTokenAsync
        converted_scopes = []
        for scope in scopes:
#            if ".default" in scope.lower():
                # Replace .default with user_impersonation
                # Handle both ".default" and ".Default" cases
#                converted_scope = scope.replace(
#                    ".default",
#                    "user_impersonation"
#                ).replace(
#                    ".Default",
#                    "user_impersonation"
#                )
#                converted_scopes.append(converted_scope)
 #           else:
                converted_scopes.append(scope)

        # Step 2: Exchange turn token for JWT token using Authorization service
        # Mirrors: ExchangeTurnTokenAsync call in C# (lines 39-40)
        token_response = await self._user_authorization.exchange_token(
            context=self._turn_context,
            exchange_connection='SERVICE_CONNECTION',
            scopes=converted_scopes,
            auth_handler_id=self._handler_name
        )

        if not token_response or not token_response.token:
            raise RuntimeError(
                "Token exchange failed - no token returned from Authorization service"
            )

        jwt_token = token_response.token

        # Step 3: Parse JWT to extract expiration time
        # Mirrors: ReadJwtToken and expiration extraction (lines 42-48 in C#)
        try:
            # Decode JWT without verification (we trust the Authorization service)
            # This matches the C# JwtSecurityTokenHandler.ReadJwtToken behavior
            decoded = jwt.decode(
                jwt_token,
                options={"verify_signature": False}
            )

            # Extract expiration claim (Unix timestamp)
            # Mirrors: jwt.Payload.Expiration in C# (line 45)
            exp_claim = decoded.get("exp")
            if exp_claim is None:
                raise RuntimeError("JWT does not contain an 'exp' claim")

            # Convert to integer Unix timestamp (Azure SDK expects int)
            # Mirrors: DateTimeOffset.FromUnixTimeSeconds in C# (line 48)
            expires_on = int(exp_claim)

        except Exception as ex:
            raise RuntimeError(
                f"Failed to parse JWT token expiration: {ex}"
            ) from ex

        # Step 4: Return AccessToken with token and expiration
        # Mirrors: return new AccessToken(jwtToken, expiresOn) in C# (line 50)
        return AccessToken(token=jwt_token, expires_on=expires_on)
    
    async def close(
        self,
    ) -> None:
        """
        Close any resources held by the token wrapper.

        This is a no-op in this implementation but is provided for
        compatibility with TokenCredential patterns that may require cleanup.

        Mirrors: DisposeAsync pattern in C# (not explicitly shown in original)
        """
        pass