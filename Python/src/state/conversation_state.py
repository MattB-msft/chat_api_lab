"""
In-memory conversation state storage.

Provides thread-safe storage for conversation state across turns.
"""

import asyncio
from typing import Dict, Any, Optional


class ConversationStateManager:
    """
    In-memory conversation state storage with async locking.

    LAB SIMPLIFICATION: Uses in-memory dict storage.
    PRODUCTION: Use Redis or another distributed cache.
    """

    def __init__(self):
        """Initialize state storage and lock."""
        self._storage: Dict[str, Dict[str, Any]] = {}
        self._lock = asyncio.Lock()

    async def get_state(self, conversation_id: str) -> Dict[str, Any]:
        """
        Get conversation state for a given conversation ID.

        Args:
            conversation_id: Unique conversation identifier.

        Returns:
            Dictionary of conversation state. Returns empty dict if not found.
        """
        async with self._lock:
            return self._storage.get(conversation_id, {}).copy()

    async def set_state(self, conversation_id: str, state: Dict[str, Any]) -> None:
        """
        Set conversation state for a given conversation ID.

        Args:
            conversation_id: Unique conversation identifier.
            state: Dictionary of state to store.
        """
        async with self._lock:
            self._storage[conversation_id] = state.copy()

    async def update_state(self, conversation_id: str, updates: Dict[str, Any]) -> None:
        """
        Update specific keys in conversation state.

        Args:
            conversation_id: Unique conversation identifier.
            updates: Dictionary of keys to update.
        """
        async with self._lock:
            if conversation_id not in self._storage:
                self._storage[conversation_id] = {}
            self._storage[conversation_id].update(updates)

    async def get_value(self, conversation_id: str, key: str, default: Any = None) -> Any:
        """
        Get a specific value from conversation state.

        Args:
            conversation_id: Unique conversation identifier.
            key: State key to retrieve.
            default: Default value if key not found.

        Returns:
            Value from state or default.
        """
        async with self._lock:
            state = self._storage.get(conversation_id, {})
            return state.get(key, default)

    async def set_value(self, conversation_id: str, key: str, value: Any) -> None:
        """
        Set a specific value in conversation state.

        Args:
            conversation_id: Unique conversation identifier.
            key: State key to set.
            value: Value to store.
        """
        async with self._lock:
            if conversation_id not in self._storage:
                self._storage[conversation_id] = {}
            self._storage[conversation_id][key] = value

    async def clear_state(self, conversation_id: str) -> None:
        """
        Clear all state for a conversation.

        Args:
            conversation_id: Unique conversation identifier.
        """
        async with self._lock:
            if conversation_id in self._storage:
                del self._storage[conversation_id]
