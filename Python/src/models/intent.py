"""
Intent models for agent orchestration.

Defines intent types and intent classification results.
"""

from dataclasses import dataclass
from enum import Enum
from typing import Optional


class IntentType(str, Enum):
    """Types of user intents that can be classified."""

    M365_EMAIL = "M365Email"
    M365_CALENDAR = "M365Calendar"
    M365_FILES = "M365Files"
    M365_PEOPLE = "M365People"
    GENERAL_KNOWLEDGE = "GeneralKnowledge"


@dataclass
class Intent:
    """Represents a classified user intent with query details."""

    type: IntentType
    query: str
    confidence: Optional[float] = None

    @property
    def is_m365_intent(self) -> bool:
        """Check if this intent requires M365 Copilot API access."""
        return self.type in (
            IntentType.M365_EMAIL,
            IntentType.M365_CALENDAR,
            IntentType.M365_FILES,
            IntentType.M365_PEOPLE
        )

    def __post_init__(self):
        """Ensure type is converted to IntentType enum."""
        if isinstance(self.type, str):
            self.type = IntentType(self.type)
