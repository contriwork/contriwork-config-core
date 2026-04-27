"""Helpers for unwrapping ``pydantic.SecretStr`` config fields safely.

The two helpers below centralize a pattern that consumers were ending up
writing at every read site: an ``if … else ""`` guard around
``cfg.field.get_secret_value()`` for fields that are typed
``SecretStr | None``. Forgetting the guard leads to a runtime
``AttributeError: 'NoneType' object has no attribute 'get_secret_value'``,
which is a sharp edge for an otherwise typed config surface.

Three-language parity: the .NET adapter exposes ``SecretStrOrEmpty`` /
``SecretStrRequired`` and the TypeScript adapter exposes
``secretStrOrEmpty`` / ``secretStrRequired``. Their input shape differs
(plain ``string?`` in C# / TS vs. ``SecretStr | None`` in Python) because
``SecretStr`` is a pydantic-specific type with no stdlib equivalent.
"""

from __future__ import annotations

from pydantic import SecretStr


def secret_str_or_empty(value: SecretStr | None) -> str:
    """Return the unwrapped secret string, or ``""`` if ``value`` is ``None``.

    Raises ``TypeError`` for any non-``SecretStr``, non-``None`` input —
    silently coercing a plain ``str`` here would mask a schema bug where
    the field was typed as ``str`` instead of ``SecretStr`` and would
    surprise downstream code that expected the ``SecretStr`` repr-redaction
    semantics.
    """
    if value is None:
        return ""
    if not isinstance(value, SecretStr):
        raise TypeError(
            f"secret_str_or_empty expected pydantic.SecretStr or None, "
            f"got {type(value).__name__}"
        )
    return value.get_secret_value()


def secret_str_required(value: SecretStr | None, field_name: str) -> str:
    """Same as :func:`secret_str_or_empty` but raises when the field is ``None``.

    The ``field_name`` is included verbatim in the raised
    :class:`ValueError` so the failure message points the caller at the
    config field that was supposed to be set.
    """
    if value is None:
        raise ValueError(f"required secret field {field_name!r} is None")
    if not isinstance(value, SecretStr):
        raise TypeError(
            f"secret_str_required expected pydantic.SecretStr or None, "
            f"got {type(value).__name__}"
        )
    return value.get_secret_value()
