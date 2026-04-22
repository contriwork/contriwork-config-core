"""SchemaAdapter protocol and the built-in Pydantic adapter.

ConfigCore does not bundle a schema library. An adapter is one method wide —
callers who want zod-like validation, attrs, msgspec, etc. can write their
own. The built-in :class:`PydanticAdapter` wraps a pydantic ``BaseModel``
class.
"""

from __future__ import annotations

from typing import Any, Protocol

from pydantic import BaseModel, ValidationError

from .errors import ValidationFailed


class SchemaAdapter[T](Protocol):
    """Validate a dict against a schema and return a typed instance."""

    def validate(self, data: dict[str, Any]) -> T:
        """Validate ``data``; return the typed result or raise :class:`ValidationFailed`."""
        ...


class PydanticAdapter[ModelT: BaseModel]:
    """Adapt a pydantic ``BaseModel`` class to the :class:`SchemaAdapter` protocol."""

    def __init__(self, model: type[ModelT]) -> None:
        self._model = model

    def validate(self, data: dict[str, Any]) -> ModelT:
        try:
            return self._model.model_validate(data)
        except ValidationError as e:
            raise ValidationFailed(
                f"config failed validation against {self._model.__name__}: "
                f"{e.error_count()} error(s)",
                details=e.errors(),
            ) from e
