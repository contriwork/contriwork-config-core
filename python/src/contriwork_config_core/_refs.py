"""Internal secret-reference walker.

Grammar:
- ``${scheme:value}`` — a reference. ``scheme`` is ``[a-z][a-z0-9_]*``; ``value``
  is any run of chars except ``}``.
- ``$${literal}`` — escape. Resolves to a literal ``${literal}`` with no
  further processing.

Only string leaves of the config tree are scanned. Numbers, booleans, and
lists-of-non-strings pass through unchanged. A literal ``${env:X}`` stored
as an int in the source is a bug in the source, not our problem to
interpret.
"""

from __future__ import annotations

import re
from typing import TYPE_CHECKING, Any

from .errors import SecretRefMalformed

if TYPE_CHECKING:
    from .resolvers import SecretResolver


# A placeholder unlikely to appear in real config values. Used to hide
# ``$${...}`` escapes from the reference regex during a single pass.
_ESCAPE_SENTINEL = "\x00CCC_ESCAPED_BRACE_\x00"

_REF_RE = re.compile(r"\$\{([a-z][a-z0-9_]*):([^}]*)\}")


async def resolve_refs(data: Any, resolver: SecretResolver | None) -> Any:
    """Walk ``data``; resolve ``${scheme:value}`` in string leaves.

    When ``resolver`` is ``None`` the walk is skipped entirely and the input
    is returned unchanged (as documented in CONTRACT.md).
    """
    if resolver is None:
        return data
    if isinstance(data, dict):
        return {k: await resolve_refs(v, resolver) for k, v in data.items()}
    if isinstance(data, list):
        return [await resolve_refs(item, resolver) for item in data]
    if isinstance(data, str):
        return await _resolve_string(data, resolver)
    return data


async def _resolve_string(text: str, resolver: SecretResolver) -> str:
    # 1. Hide $${...} escapes so the ref regex won't see them.
    protected = text.replace("$${", _ESCAPE_SENTINEL)

    # 2. Any remaining "${" that doesn't match the valid-ref regex is malformed.
    _assert_no_malformed_refs(protected, original=text)

    # 3. Replace each valid ${scheme:value} with its resolved value.
    parts: list[str] = []
    last_end = 0
    for m in _REF_RE.finditer(protected):
        scheme = m.group(1)
        value = m.group(2)
        resolved = await resolver.resolve(scheme, value)
        parts.append(protected[last_end : m.start()])
        parts.append(resolved)
        last_end = m.end()
    parts.append(protected[last_end:])

    result = "".join(parts)

    # 4. Restore escapes: sentinel → literal "${".
    return result.replace(_ESCAPE_SENTINEL, "${")


def _assert_no_malformed_refs(protected: str, *, original: str) -> None:
    idx = 0
    while True:
        start = protected.find("${", idx)
        if start == -1:
            return
        m = _REF_RE.match(protected, start)
        if m is None:
            raise SecretRefMalformed(
                f"malformed secret reference at offset {start} in {original!r}"
            )
        idx = m.end()
