"""Internal deep-merge. Not part of the public surface."""

from __future__ import annotations

from typing import Any


def deep_merge(base: dict[str, Any], overlay: dict[str, Any]) -> dict[str, Any]:
    """Deep-merge two dicts; overlay wins.

    - Dicts recurse key-wise.
    - Lists, scalars, and type mismatches replace.
    - Neither argument is mutated.
    """
    result: dict[str, Any] = dict(base)
    for key, overlay_val in overlay.items():
        base_val = result.get(key)
        if isinstance(base_val, dict) and isinstance(overlay_val, dict):
            result[key] = deep_merge(base_val, overlay_val)
        else:
            result[key] = overlay_val
    return result
