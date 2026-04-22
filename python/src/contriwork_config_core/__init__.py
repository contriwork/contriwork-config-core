"""contriwork-config-core — Python adapter.

Public surface re-exports from :mod:`contriwork_config_core.port`. Do not
import concrete adapter classes from outside — they are internal detail.
"""

from __future__ import annotations

from importlib.metadata import PackageNotFoundError, version

from .port import ConfigCorePort

__all__ = ["ConfigCorePort", "__version__"]

try:
    __version__ = version("contriwork-config-core")
except PackageNotFoundError:
    __version__ = "0.0.0"
