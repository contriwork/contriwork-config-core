"""SecretResolver protocol and built-in implementations.

See CONTRACT.md §Port/`SecretResolver protocol`. Each resolver handles one or
more schemes of ``${scheme:value}``. Unknown schemes raise
:class:`SecretSchemeUnsupported`; recognized-but-unresolvable values raise
:class:`SecretRefUnresolved`. :class:`ChainResolver` composes several.
"""

from __future__ import annotations

import os
from pathlib import Path
from typing import Protocol, runtime_checkable

from .errors import SecretRefUnresolved, SecretSchemeUnsupported


@runtime_checkable
class SecretResolver(Protocol):
    """Resolve a ``${scheme:value}`` reference to a plain string."""

    async def resolve(self, scheme: str, value: str) -> str:
        """Return the resolved secret or raise.

        - :class:`SecretSchemeUnsupported` if this resolver does not handle
          ``scheme``. :class:`ChainResolver` relies on this signal to try
          the next resolver.
        - :class:`SecretRefUnresolved` if ``scheme`` is handled but ``value``
          cannot be produced (env var not set, file missing, etc.).
        """
        ...


class EnvResolver:
    """Resolve ``${env:NAME}`` from the process environment."""

    scheme = "env"

    async def resolve(self, scheme: str, value: str) -> str:
        if scheme != self.scheme:
            raise SecretSchemeUnsupported(f"EnvResolver does not handle scheme {scheme!r}")
        if not value:
            raise SecretRefUnresolved("${env:} requires a variable name")
        resolved = os.environ.get(value)
        if resolved is None:
            raise SecretRefUnresolved(f"env var not set: {value}")
        return resolved


class FileResolver:
    """Resolve ``${file:path}`` by reading the file's contents.

    Trailing whitespace (incl. newline) is stripped from the result — this
    matches the common "secret in a single-line file" pattern used by
    Docker secrets and ``/run/secrets/``. If ``base_dir`` is provided,
    relative paths resolve against it; absolute paths are honored as-is.
    """

    scheme = "file"

    def __init__(self, base_dir: str | Path | None = None) -> None:
        self._base_dir = Path(base_dir) if base_dir is not None else None

    async def resolve(self, scheme: str, value: str) -> str:
        if scheme != self.scheme:
            raise SecretSchemeUnsupported(f"FileResolver does not handle scheme {scheme!r}")
        if not value:
            raise SecretRefUnresolved("${file:} requires a path")
        path = Path(value)
        if not path.is_absolute() and self._base_dir is not None:
            path = self._base_dir / path
        try:
            text = path.read_text(encoding="utf-8")
        except FileNotFoundError:
            raise SecretRefUnresolved(f"secret file not found: {path}") from None
        except OSError as e:
            raise SecretRefUnresolved(f"cannot read secret file {path}: {e}") from e
        return text.rstrip()


class ChainResolver:
    """Try each resolver in order; first one that handles the scheme wins.

    - If a resolver raises :class:`SecretSchemeUnsupported`, move on.
    - Any other exception propagates immediately (including
      :class:`SecretRefUnresolved`).
    - If no resolver claims the scheme, raise :class:`SecretSchemeUnsupported`
      naming the scheme.
    """

    def __init__(self, *resolvers: SecretResolver) -> None:
        self._resolvers = resolvers

    async def resolve(self, scheme: str, value: str) -> str:
        for r in self._resolvers:
            try:
                return await r.resolve(scheme, value)
            except SecretSchemeUnsupported:
                continue
        raise SecretSchemeUnsupported(f"no resolver registered for scheme {scheme!r}")
