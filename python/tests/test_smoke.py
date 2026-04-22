"""Smoke tests — verify package imports and port is reachable."""

from __future__ import annotations


def test_package_imports() -> None:
    import contriwork_config_core

    assert contriwork_config_core.__version__


def test_port_is_exported() -> None:
    from contriwork_config_core import ConfigCorePort

    assert ConfigCorePort is not None
    assert hasattr(ConfigCorePort, "example")
