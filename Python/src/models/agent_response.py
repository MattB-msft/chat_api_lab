"""
Agent response models for multi-agent orchestration.

Represents responses from individual agents during execution.
"""

from dataclasses import dataclass, field
from typing import Optional, Dict, Any

from .intent import IntentType


@dataclass
class AgentResponse:
    """Response from an individual agent execution."""

    agent: str
    intent_type: IntentType
    content: str
    success: bool = True
    error: Optional[str] = None
    metadata: Dict[str, Any] = field(default_factory=dict)

    def __post_init__(self):
        """Ensure intent_type is converted to IntentType enum."""
        if isinstance(self.intent_type, str):
            self.intent_type = IntentType(self.intent_type)

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for serialization."""
        return {
            "agent": self.agent,
            "intent_type": self.intent_type.value,
            "content": self.content,
            "success": self.success,
            "error": self.error,
            "metadata": self.metadata
        }
