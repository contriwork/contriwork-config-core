"""Tests for the SecretStr unwrap helpers."""

from __future__ import annotations

import pytest
from pydantic import SecretStr

from contriwork_config_core import secret_str_or_empty, secret_str_required

# ── secret_str_or_empty ─────────────────────────────────────────────


def test_or_empty_unwraps_secret_str() -> None:
    assert secret_str_or_empty(SecretStr("hunter2")) == "hunter2"


def test_or_empty_returns_empty_string_for_none() -> None:
    assert secret_str_or_empty(None) == ""


def test_or_empty_rejects_plain_str() -> None:
    # Don't silently coerce: a plain str signals a schema bug where the field
    # wasn't typed as SecretStr in the first place.
    with pytest.raises(TypeError, match="SecretStr"):
        secret_str_or_empty("hunter2")  # type: ignore[arg-type]


def test_or_empty_rejects_other_types() -> None:
    with pytest.raises(TypeError, match="SecretStr"):
        secret_str_or_empty(42)  # type: ignore[arg-type]


# ── secret_str_required ─────────────────────────────────────────────


def test_required_unwraps_secret_str() -> None:
    assert secret_str_required(SecretStr("hunter2"), "db_password") == "hunter2"


def test_required_raises_value_error_with_field_name_for_none() -> None:
    with pytest.raises(ValueError, match="db_password"):
        secret_str_required(None, "db_password")


def test_required_rejects_plain_str() -> None:
    with pytest.raises(TypeError, match="SecretStr"):
        secret_str_required("hunter2", "db_password")  # type: ignore[arg-type]
