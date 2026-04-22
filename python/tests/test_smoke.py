"""Smoke tests — verify package imports and the v1 surface is reachable."""

from __future__ import annotations


def test_package_imports() -> None:
    import contriwork_config_core

    assert contriwork_config_core.__version__


def test_public_surface_is_exported() -> None:
    from contriwork_config_core import (
        ChainResolver,
        ConfigError,
        EnvResolver,
        EnvSource,
        FileResolver,
        FileSource,
        InMemorySource,
        PydanticAdapter,
        SchemaAdapter,
        SecretResolver,
        Source,
        load_config,
    )

    # Non-null sanity — the import itself is the real test.
    assert all(
        obj is not None
        for obj in (
            ChainResolver,
            ConfigError,
            EnvResolver,
            EnvSource,
            FileResolver,
            FileSource,
            InMemorySource,
            PydanticAdapter,
            SchemaAdapter,
            SecretResolver,
            Source,
            load_config,
        )
    )


def test_v0_port_is_removed() -> None:
    """ConfigCorePort was the v0 placeholder; v1 replaces it with load_config."""
    import contriwork_config_core

    assert not hasattr(contriwork_config_core, "ConfigCorePort")
