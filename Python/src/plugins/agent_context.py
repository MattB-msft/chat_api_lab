"""
Agent context for passing state and dependencies to plugins.

Provides plugins with access to turn context, conversation state, and authentication.
"""

from dataclasses import dataclass
from typing import Dict, Any, Optional, Callable, Awaitable
from microsoft_agents.hosting.core import (
    Authorization,
    TurnContext,
    TurnState,
)
import logging


@dataclass
class AgentContext:
    """
    Context object passed to plugins during execution.

    This provides plugins with access to request state, conversation storage,
    authentication handlers, and logging capabilities.
    """

    context: TurnContext
    state: TurnState
    connection_name: str
    user_auth: Authorization
    auth_handler_name: str
    logger: Optional[logging.Logger] = None

    def __post_init__(self):
        """Initialize logger if not provided."""
        if self.logger is None:
            self.logger = logging.getLogger(__name__)
