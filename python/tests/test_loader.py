"""End-to-end: load_config composes sources + resolver + schema."""

from __future__ import annotations

from pathlib import Path

import pytest
from pydantic import BaseModel

from contriwork_config_core import (
    EnvResolver,
    EnvSource,
    FileSource,
    InMemorySource,
    PydanticAdapter,
    SecretRefUnresolved,
    ValidationFailed,
    load_config,
)


class AppConfig(BaseModel):
    db_url: str
    debug: bool = False


async def test_single_source_roundtrip() -> None:
    cfg = await load_config(
        schema=PydanticAdapter(AppConfig),
        sources=[InMemorySource({"db_url": "sqlite://x", "debug": True})],
    )
    assert cfg.db_url == "sqlite://x"
    assert cfg.debug is True


async def test_later_source_overrides_earlier() -> None:
    cfg = await load_config(
        schema=PydanticAdapter(AppConfig),
        sources=[
            InMemorySource({"db_url": "sqlite://default", "debug": False}),
            InMemorySource({"debug": True}),
        ],
    )
    assert cfg.db_url == "sqlite://default"  # not overridden
    assert cfg.debug is True  # overridden


async def test_no_sources_raises() -> None:
    with pytest.raises(ValidationFailed):
        await load_config(schema=PydanticAdapter(AppConfig), sources=[])


async def test_env_source_populates_config(monkeypatch: pytest.MonkeyPatch) -> None:
    # With default separator "__", "APP_DB_URL" has no "__" → flat key "db_url".
    monkeypatch.setenv("APP_DB_URL", "pg://app")
    monkeypatch.setenv("APP_DEBUG", "true")
    cfg = await load_config(
        schema=PydanticAdapter(AppConfig),
        sources=[EnvSource(prefix="APP_")],
    )
    assert cfg.db_url == "pg://app"
    assert cfg.debug is True  # pydantic coerces "true" → True


async def test_file_source_populates_config(tmp_path: Path) -> None:
    p = tmp_path / "cfg.yaml"
    p.write_text("db_url: sqlite://file.db\ndebug: false\n", encoding="utf-8")
    cfg = await load_config(schema=PydanticAdapter(AppConfig), sources=[FileSource(p)])
    assert cfg.db_url == "sqlite://file.db"
    assert cfg.debug is False


async def test_secret_ref_resolves_default_env(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("DB_URL_SECRET", "pg://from-secret")
    cfg = await load_config(
        schema=PydanticAdapter(AppConfig),
        sources=[InMemorySource({"db_url": "${env:DB_URL_SECRET}"})],
    )
    assert cfg.db_url == "pg://from-secret"


async def test_explicit_none_disables_refs() -> None:
    cfg = await load_config(
        schema=PydanticAdapter(AppConfig),
        sources=[InMemorySource({"db_url": "${env:NOT_INTERPOLATED}"})],
        resolver=None,
    )
    # When resolver=None, the literal string passes through unchanged.
    assert cfg.db_url == "${env:NOT_INTERPOLATED}"


async def test_explicit_resolver_used(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("X", "y")
    cfg = await load_config(
        schema=PydanticAdapter(AppConfig),
        sources=[InMemorySource({"db_url": "${env:X}"})],
        resolver=EnvResolver(),
    )
    assert cfg.db_url == "y"


async def test_unresolved_secret_propagates(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.delenv("MISSING", raising=False)
    with pytest.raises(SecretRefUnresolved):
        await load_config(
            schema=PydanticAdapter(AppConfig),
            sources=[InMemorySource({"db_url": "${env:MISSING}"})],
        )


async def test_missing_required_field_fails_validation() -> None:
    with pytest.raises(ValidationFailed):
        await load_config(
            schema=PydanticAdapter(AppConfig),
            sources=[InMemorySource({"debug": True})],  # no db_url
        )


async def test_file_plus_env_merges(monkeypatch: pytest.MonkeyPatch, tmp_path: Path) -> None:
    p = tmp_path / "cfg.yaml"
    p.write_text("db_url: sqlite://file-default\ndebug: false\n", encoding="utf-8")
    monkeypatch.setenv("APP_DEBUG", "true")
    cfg = await load_config(
        schema=PydanticAdapter(AppConfig),
        sources=[FileSource(p), EnvSource(prefix="APP_")],
    )
    # File provides default; env overrides debug only.
    assert cfg.db_url == "sqlite://file-default"
    assert cfg.debug is True
