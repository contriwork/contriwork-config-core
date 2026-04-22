"""Source protocol and built-in implementations.

See CONTRACT.md §Port/`Source protocol`. A source exposes a snapshot of
configuration keys as a nested dict. Implementations MAY read from the
environment, files, or in-memory data; external secret managers ship as
separate adapter packages.
"""

from __future__ import annotations

import json
import os
import tomllib
from pathlib import Path
from typing import Any, Literal, Protocol, runtime_checkable

import yaml

from .errors import SourceParseFailed, SourceUnavailable

Format = Literal["yaml", "json", "toml"]


@runtime_checkable
class Source(Protocol):
    """A source of configuration keys. Cross-language: C# ``ISource``, TS ``Source``."""

    async def snapshot(self) -> dict[str, Any]:
        """Return a fresh dict of this source's current keys.

        Raises :class:`SourceUnavailable` or :class:`SourceParseFailed` per
        CONTRACT §Error Taxonomy.
        """
        ...


class InMemorySource:
    """A source backed by a dict. Primarily for tests."""

    def __init__(self, data: dict[str, Any]) -> None:
        self._data = data

    async def snapshot(self) -> dict[str, Any]:
        # Deep-copy via JSON round-trip is overkill and would reject
        # non-JSON values. Return a shallow copy at the top level; nested
        # dicts are shared by reference, which is fine because the caller
        # (``load_config``) merges into a new dict without mutating ours.
        return dict(self._data)


class EnvSource:
    """Read the process environment and map flat keys to a nested dict.

    Keys starting with ``prefix`` (case-sensitive) are collected. The
    prefix is stripped, the result is lowercased, and each ``separator``
    occurrence becomes a nested dict level. Example::

        EnvSource(prefix="APP_", separator="__").snapshot()
        # APP_DB__URL="sqlite://..." → {"db": {"url": "sqlite://..."}}
        # APP_DEBUG="true"            → {"debug": "true"}

    Values are returned as strings; coercion to int/bool/etc. is the
    schema adapter's job.
    """

    def __init__(self, prefix: str = "", separator: str = "__") -> None:
        if not separator:
            raise ValueError("separator must be a non-empty string")
        self._prefix = prefix
        self._separator = separator

    async def snapshot(self) -> dict[str, Any]:
        result: dict[str, Any] = {}
        for raw_key, value in os.environ.items():
            if not raw_key.startswith(self._prefix):
                continue
            stripped = raw_key[len(self._prefix) :]
            if not stripped:
                continue
            path = stripped.lower().split(self._separator)
            _set_nested(result, path, value)
        return result


class FileSource:
    """Load a YAML / JSON / TOML file from disk.

    Format is inferred from the extension (``.yaml`` / ``.yml`` / ``.json`` /
    ``.toml``) unless ``format`` is passed explicitly. A missing file raises
    :class:`SourceUnavailable` when ``required=True`` (the default); with
    ``required=False`` an empty dict is returned.
    """

    def __init__(
        self,
        path: str | Path,
        *,
        format: Format | None = None,
        required: bool = True,
    ) -> None:
        self._path = Path(path)
        self._format: Format = format or _infer_format(self._path)
        self._required = required

    async def snapshot(self) -> dict[str, Any]:
        try:
            raw = self._path.read_bytes()
        except FileNotFoundError:
            if not self._required:
                return {}
            raise SourceUnavailable(f"config file not found: {self._path}") from None
        except OSError as e:
            raise SourceUnavailable(f"cannot read config file {self._path}: {e}") from e

        try:
            parsed = _parse(raw, self._format)
        except Exception as e:
            raise SourceParseFailed(f"failed to parse {self._path} as {self._format}: {e}") from e
        if parsed is None:
            return {}
        if not isinstance(parsed, dict):
            raise SourceParseFailed(
                f"{self._path} ({self._format}) must contain a top-level mapping; "
                f"got {type(parsed).__name__}"
            )
        return parsed


def _set_nested(target: dict[str, Any], path: list[str], value: str) -> None:
    cursor = target
    for key in path[:-1]:
        existing = cursor.get(key)
        if not isinstance(existing, dict):
            existing = {}
            cursor[key] = existing
        cursor = existing
    cursor[path[-1]] = value


def _infer_format(path: Path) -> Format:
    suffix = path.suffix.lower()
    if suffix in (".yaml", ".yml"):
        return "yaml"
    if suffix == ".json":
        return "json"
    if suffix == ".toml":
        return "toml"
    raise SourceParseFailed(
        f"cannot infer format from {path.name}; pass format= explicitly (yaml / json / toml)"
    )


def _parse(raw: bytes, fmt: Format) -> Any:
    if fmt == "yaml":
        return yaml.safe_load(raw)
    if fmt == "json":
        if not raw.strip():
            return None
        return json.loads(raw)
    if fmt == "toml":
        return tomllib.loads(raw.decode("utf-8"))
    # Unreachable: Format is a Literal.
    raise SourceParseFailed(f"unsupported format: {fmt}")
