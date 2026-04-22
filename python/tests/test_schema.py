"""PydanticAdapter wraps pydantic ValidationError into ValidationFailed."""

from __future__ import annotations

import pytest
from pydantic import BaseModel, Field

from contriwork_config_core import PydanticAdapter, ValidationFailed


class AppConfig(BaseModel):
    db_url: str
    debug: bool = False
    pool_size: int = Field(ge=1)


def test_valid_data_returns_typed_model() -> None:
    cfg = PydanticAdapter(AppConfig).validate(
        {"db_url": "sqlite://x", "debug": True, "pool_size": 5}
    )
    assert isinstance(cfg, AppConfig)
    assert cfg.db_url == "sqlite://x"
    assert cfg.debug is True
    assert cfg.pool_size == 5


def test_missing_required_raises_validation_failed() -> None:
    with pytest.raises(ValidationFailed) as exc:
        PydanticAdapter(AppConfig).validate({"debug": True, "pool_size": 5})
    assert exc.value.code == "VALIDATION_FAILED"
    assert exc.value.details is not None
    assert isinstance(exc.value.details, list)


def test_constraint_violation_raises() -> None:
    with pytest.raises(ValidationFailed):
        PydanticAdapter(AppConfig).validate({"db_url": "x", "debug": False, "pool_size": 0})


def test_original_error_preserved_via_cause() -> None:
    with pytest.raises(ValidationFailed) as exc:
        PydanticAdapter(AppConfig).validate({})
    assert exc.value.__cause__ is not None
