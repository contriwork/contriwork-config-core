"""Error taxonomy — mirrors CONTRACT.md §Error Taxonomy (v1).

Every error has a stable ``code`` attribute. The code names are the contract;
the Python class names are local convention and may be renamed without a
contract bump, but the codes never are.
"""

from __future__ import annotations

from typing import Any


class ConfigError(Exception):
    """Base class for all ConfigCore errors."""

    code: str = ""

    def __init__(self, message: str, *, details: Any = None) -> None:
        super().__init__(message)
        self.details = details


class ValidationFailed(ConfigError):
    """The merged, resolved config did not validate against the schema."""

    code = "VALIDATION_FAILED"


class SourceUnavailable(ConfigError):
    """A declared source could not be opened or read."""

    code = "SOURCE_UNAVAILABLE"


class SourceParseFailed(ConfigError):
    """A source's content could not be parsed into a dict."""

    code = "SOURCE_PARSE_FAILED"


class SecretRefMalformed(ConfigError):
    """A ``${...}`` reference did not match the documented syntax."""

    code = "SECRET_REF_MALFORMED"


class SecretSchemeUnsupported(ConfigError):
    """A reference's scheme has no registered resolver."""

    code = "SECRET_SCHEME_UNSUPPORTED"


class SecretRefUnresolved(ConfigError):
    """A resolver accepted the scheme but could not produce a value."""

    code = "SECRET_REF_UNRESOLVED"
