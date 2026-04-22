"""Source implementations — Env / File / InMemory."""

from __future__ import annotations

from pathlib import Path

import pytest

from contriwork_config_core import (
    EnvSource,
    FileSource,
    InMemorySource,
    SourceParseFailed,
    SourceUnavailable,
)

# ── InMemorySource ──────────────────────────────────────────────────


async def test_inmemory_returns_data() -> None:
    src = InMemorySource({"a": 1, "b": {"c": 2}})
    snapshot = await src.snapshot()
    assert snapshot == {"a": 1, "b": {"c": 2}}


async def test_inmemory_returns_fresh_copy() -> None:
    data = {"a": 1}
    src = InMemorySource(data)
    snapshot = await src.snapshot()
    snapshot["a"] = 999
    # Original dict is not mutated by caller's modifications of the snapshot.
    assert data["a"] == 1


# ── EnvSource ───────────────────────────────────────────────────────


async def test_env_empty_prefix_picks_all(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("X__Y", "v")
    src = EnvSource()  # empty prefix
    snapshot = await src.snapshot()
    assert snapshot.get("x") == {"y": "v"}


async def test_env_prefix_filter(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("APP__DB__URL", "sqlite://app.db")
    monkeypatch.setenv("OTHER__IGNORED", "x")
    src = EnvSource(prefix="APP__")
    snapshot = await src.snapshot()
    assert snapshot == {"db": {"url": "sqlite://app.db"}}


async def test_env_custom_separator(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("APP_DB_URL", "pg://x")
    src = EnvSource(prefix="APP_", separator="_")
    snapshot = await src.snapshot()
    assert snapshot == {"db": {"url": "pg://x"}}


async def test_env_prefix_only_skipped(monkeypatch: pytest.MonkeyPatch) -> None:
    # A key equal to the prefix has nothing left after stripping; skip it.
    monkeypatch.setenv("APP_", "bogus")
    src = EnvSource(prefix="APP_")
    snapshot = await src.snapshot()
    assert snapshot == {}


async def test_env_separator_must_be_nonempty() -> None:
    with pytest.raises(ValueError):
        EnvSource(separator="")


async def test_env_flat_key_no_separator(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("APP_DEBUG", "true")
    src = EnvSource(prefix="APP_", separator="__")
    snapshot = await src.snapshot()
    assert snapshot == {"debug": "true"}


# ── FileSource ──────────────────────────────────────────────────────


async def test_file_yaml(tmp_path: Path) -> None:
    p = tmp_path / "cfg.yaml"
    p.write_text("db:\n  url: sqlite://x\n  pool: 10\n", encoding="utf-8")
    snapshot = await FileSource(p).snapshot()
    assert snapshot == {"db": {"url": "sqlite://x", "pool": 10}}


async def test_file_json(tmp_path: Path) -> None:
    p = tmp_path / "cfg.json"
    p.write_text('{"db": {"url": "sqlite://y"}}', encoding="utf-8")
    snapshot = await FileSource(p).snapshot()
    assert snapshot == {"db": {"url": "sqlite://y"}}


async def test_file_toml(tmp_path: Path) -> None:
    p = tmp_path / "cfg.toml"
    p.write_text('[db]\nurl = "sqlite://z"\npool = 5\n', encoding="utf-8")
    snapshot = await FileSource(p).snapshot()
    assert snapshot == {"db": {"url": "sqlite://z", "pool": 5}}


async def test_file_format_override(tmp_path: Path) -> None:
    p = tmp_path / "weird.cfg"
    p.write_text('{"a": 1}', encoding="utf-8")
    snapshot = await FileSource(p, format="json").snapshot()
    assert snapshot == {"a": 1}


async def test_file_missing_required_raises(tmp_path: Path) -> None:
    p = tmp_path / "nope.yaml"
    with pytest.raises(SourceUnavailable):
        await FileSource(p).snapshot()


async def test_file_missing_optional_returns_empty(tmp_path: Path) -> None:
    p = tmp_path / "nope.yaml"
    snapshot = await FileSource(p, required=False).snapshot()
    assert snapshot == {}


async def test_file_unknown_extension_raises(tmp_path: Path) -> None:
    p = tmp_path / "cfg.bin"
    p.write_text("anything", encoding="utf-8")
    with pytest.raises(SourceParseFailed):
        await FileSource(p).snapshot()


async def test_file_non_mapping_root_raises(tmp_path: Path) -> None:
    p = tmp_path / "cfg.yaml"
    p.write_text("- item1\n- item2\n", encoding="utf-8")
    with pytest.raises(SourceParseFailed):
        await FileSource(p).snapshot()


async def test_file_malformed_yaml_raises(tmp_path: Path) -> None:
    p = tmp_path / "cfg.yaml"
    p.write_text("key: value\n  bad-indent: !broken", encoding="utf-8")
    with pytest.raises(SourceParseFailed):
        await FileSource(p).snapshot()


async def test_file_empty_json_returns_empty(tmp_path: Path) -> None:
    p = tmp_path / "cfg.json"
    p.write_text("", encoding="utf-8")
    snapshot = await FileSource(p).snapshot()
    assert snapshot == {}
