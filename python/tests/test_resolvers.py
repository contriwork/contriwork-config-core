"""SecretResolver implementations — Env / File / Chain."""

from __future__ import annotations

from pathlib import Path

import pytest

from contriwork_config_core import (
    ChainResolver,
    EnvResolver,
    FileResolver,
    SecretRefUnresolved,
    SecretSchemeUnsupported,
)

# ── EnvResolver ─────────────────────────────────────────────────────


async def test_env_resolves(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("SECRET_TOKEN", "s3cr3t")
    assert await EnvResolver().resolve("env", "SECRET_TOKEN") == "s3cr3t"


async def test_env_missing_var_raises(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.delenv("DOES_NOT_EXIST", raising=False)
    with pytest.raises(SecretRefUnresolved):
        await EnvResolver().resolve("env", "DOES_NOT_EXIST")


async def test_env_empty_name_raises() -> None:
    with pytest.raises(SecretRefUnresolved):
        await EnvResolver().resolve("env", "")


async def test_env_wrong_scheme_raises() -> None:
    with pytest.raises(SecretSchemeUnsupported):
        await EnvResolver().resolve("file", "whatever")


# ── FileResolver ────────────────────────────────────────────────────


async def test_file_resolves_absolute(tmp_path: Path) -> None:
    p = tmp_path / "s.txt"
    p.write_text("mysecret\n", encoding="utf-8")
    assert await FileResolver().resolve("file", str(p)) == "mysecret"


async def test_file_strips_trailing_whitespace_only(tmp_path: Path) -> None:
    p = tmp_path / "s.txt"
    p.write_text("  value with spaces  \n\n", encoding="utf-8")
    # rstrip strips trailing whitespace/newline but preserves leading spaces.
    assert await FileResolver().resolve("file", str(p)) == "  value with spaces"


async def test_file_missing_raises(tmp_path: Path) -> None:
    with pytest.raises(SecretRefUnresolved):
        await FileResolver().resolve("file", str(tmp_path / "nope"))


async def test_file_empty_value_raises() -> None:
    with pytest.raises(SecretRefUnresolved):
        await FileResolver().resolve("file", "")


async def test_file_wrong_scheme_raises() -> None:
    with pytest.raises(SecretSchemeUnsupported):
        await FileResolver().resolve("env", "whatever")


async def test_file_relative_with_base_dir(tmp_path: Path) -> None:
    (tmp_path / "s.txt").write_text("relative-secret\n", encoding="utf-8")
    r = FileResolver(base_dir=tmp_path)
    assert await r.resolve("file", "s.txt") == "relative-secret"


# ── ChainResolver ───────────────────────────────────────────────────


async def test_chain_first_hit_wins(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("A", "env-value")
    chain = ChainResolver(EnvResolver(), FileResolver())
    assert await chain.resolve("env", "A") == "env-value"


async def test_chain_skips_unsupported_to_next(
    monkeypatch: pytest.MonkeyPatch, tmp_path: Path
) -> None:
    # EnvResolver rejects scheme="file"; FileResolver handles it.
    (tmp_path / "s.txt").write_text("chained\n", encoding="utf-8")
    chain = ChainResolver(EnvResolver(), FileResolver())
    assert await chain.resolve("file", str(tmp_path / "s.txt")) == "chained"


async def test_chain_no_handler_raises() -> None:
    chain = ChainResolver(EnvResolver(), FileResolver())
    with pytest.raises(SecretSchemeUnsupported):
        await chain.resolve("vault", "some/path")


async def test_chain_propagates_unresolved(monkeypatch: pytest.MonkeyPatch) -> None:
    # EnvResolver claims scheme="env" and raises SecretRefUnresolved — chain must
    # NOT swallow it and try the next resolver; scheme-match wins first handler.
    monkeypatch.delenv("NO_SUCH_VAR", raising=False)
    chain = ChainResolver(EnvResolver(), FileResolver())
    with pytest.raises(SecretRefUnresolved):
        await chain.resolve("env", "NO_SUCH_VAR")


async def test_chain_empty_raises() -> None:
    with pytest.raises(SecretSchemeUnsupported):
        await ChainResolver().resolve("env", "X")
