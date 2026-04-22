"""Deep-merge behavior per CONTRACT §Behavior/Precedence and merging."""

from __future__ import annotations

from contriwork_config_core._merge import deep_merge


def test_overlay_replaces_scalar() -> None:
    assert deep_merge({"a": 1}, {"a": 2}) == {"a": 2}


def test_overlay_adds_new_key() -> None:
    assert deep_merge({"a": 1}, {"b": 2}) == {"a": 1, "b": 2}


def test_nested_dicts_recurse() -> None:
    base = {"db": {"url": "x", "pool": 5}}
    overlay = {"db": {"pool": 10}}
    assert deep_merge(base, overlay) == {"db": {"url": "x", "pool": 10}}


def test_list_replaces_not_concats() -> None:
    base = {"tags": ["a", "b"]}
    overlay = {"tags": ["c"]}
    assert deep_merge(base, overlay) == {"tags": ["c"]}


def test_dict_replaces_scalar_when_types_mismatch() -> None:
    base = {"x": 1}
    overlay = {"x": {"nested": True}}
    assert deep_merge(base, overlay) == {"x": {"nested": True}}


def test_scalar_replaces_dict_when_types_mismatch() -> None:
    base = {"x": {"nested": True}}
    overlay = {"x": "flat"}
    assert deep_merge(base, overlay) == {"x": "flat"}


def test_inputs_are_not_mutated() -> None:
    base = {"a": {"b": 1}}
    overlay = {"a": {"c": 2}}
    merged = deep_merge(base, overlay)
    merged["a"]["b"] = 999
    assert base["a"]["b"] == 1
