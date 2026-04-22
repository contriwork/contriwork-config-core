"""Error-code stability: every contract code has a matching subclass."""

from __future__ import annotations

import pytest

from contriwork_config_core import (
    ConfigError,
    SecretRefMalformed,
    SecretRefUnresolved,
    SecretSchemeUnsupported,
    SourceParseFailed,
    SourceUnavailable,
    ValidationFailed,
)


@pytest.mark.parametrize(
    ("cls", "code"),
    [
        (ValidationFailed, "VALIDATION_FAILED"),
        (SourceUnavailable, "SOURCE_UNAVAILABLE"),
        (SourceParseFailed, "SOURCE_PARSE_FAILED"),
        (SecretRefMalformed, "SECRET_REF_MALFORMED"),
        (SecretSchemeUnsupported, "SECRET_SCHEME_UNSUPPORTED"),
        (SecretRefUnresolved, "SECRET_REF_UNRESOLVED"),
    ],
)
def test_code_is_stable(cls: type[ConfigError], code: str) -> None:
    assert cls.code == code
    assert issubclass(cls, ConfigError)


def test_details_are_optional() -> None:
    err = ValidationFailed("boom")
    assert err.details is None


def test_details_round_trip() -> None:
    payload = [{"loc": ("field",), "msg": "required"}]
    err = ValidationFailed("validation failed", details=payload)
    assert err.details == payload
