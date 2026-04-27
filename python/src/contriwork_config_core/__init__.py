"""contriwork-config-core — Python adapter.

Cross-language static configuration loader. See CONTRACT.md for the
language-agnostic specification; this module is the Python binding.

Example::

    from pydantic import BaseModel
    from contriwork_config_core import (
        load_config, EnvSource, FileSource, PydanticAdapter,
    )

    class AppConfig(BaseModel):
        db_url: str
        debug: bool = False

    cfg = await load_config(
        schema=PydanticAdapter(AppConfig),
        sources=[FileSource("./cfg.yml"), EnvSource(prefix="APP_")],
    )
"""

from __future__ import annotations

from importlib.metadata import PackageNotFoundError, version

from .errors import (
    ConfigError,
    SecretRefMalformed,
    SecretRefUnresolved,
    SecretSchemeUnsupported,
    SourceParseFailed,
    SourceUnavailable,
    ValidationFailed,
)
from .loader import load_config
from .resolvers import (
    ChainResolver,
    EnvResolver,
    FileResolver,
    NullResolver,
    SecretResolver,
)
from .schema import PydanticAdapter, SchemaAdapter
from .secrets import secret_str_or_empty, secret_str_required
from .sources import EnvSource, FileSource, InMemorySource, Source

__all__ = [
    "ChainResolver",
    "ConfigError",
    "EnvResolver",
    "EnvSource",
    "FileResolver",
    "FileSource",
    "InMemorySource",
    "NullResolver",
    "PydanticAdapter",
    "SchemaAdapter",
    "SecretRefMalformed",
    "SecretRefUnresolved",
    "SecretResolver",
    "SecretSchemeUnsupported",
    "Source",
    "SourceParseFailed",
    "SourceUnavailable",
    "ValidationFailed",
    "__version__",
    "load_config",
    "secret_str_or_empty",
    "secret_str_required",
]

try:
    __version__ = version("contriwork-config-core")
except PackageNotFoundError:
    __version__ = "0.0.0"
