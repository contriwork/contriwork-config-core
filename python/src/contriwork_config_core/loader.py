"""Public entry point: ``load_config``.

Order of operations (CONTRACT.md §Behavior):
1. ``Source.snapshot()`` for each source, in declared order.
2. Deep-merge; later sources override earlier.
3. Walk string leaves and resolve ``${scheme:value}`` via the resolver.
4. Validate the merged, resolved dict against the schema.
"""

from __future__ import annotations

from collections.abc import Sequence
from typing import cast

from ._merge import deep_merge
from ._refs import resolve_refs
from .errors import ValidationFailed
from .resolvers import EnvResolver, NullResolver, SecretResolver
from .schema import SchemaAdapter
from .sources import Source


class _DefaultResolverSentinel:
    """Marker for ``resolver=<omitted>``; distinguishable from explicit ``None``.

    Hidden from the public signature via :func:`typing.cast` so IDE hover
    and ``inspect.signature`` show the honest type ``SecretResolver | None``;
    runtime distinguishes "omitted" from "explicit None" via this object's
    identity.
    """


# Cast at module scope (not in the parameter default) so ruff B008 is happy
# and the public signature reads as the honest `SecretResolver | None`.
_DEFAULT_RESOLVER: SecretResolver | None = cast("SecretResolver | None", _DefaultResolverSentinel())


async def load_config[T](
    schema: SchemaAdapter[T],
    sources: Sequence[Source],
    resolver: SecretResolver | None = _DEFAULT_RESOLVER,
) -> T:
    """Load, merge, resolve, and validate.

    Args:
        schema: A :class:`SchemaAdapter` that validates the merged dict.
        sources: Ordered list of :class:`Source` instances. Later sources
            override earlier ones. Empty list is an error.
        resolver: Optional :class:`SecretResolver` for ``${...}`` refs.

            - **Omitted** (the parameter is not passed): defaults to
              :class:`EnvResolver` — env-only secret resolution, the
              v0.1.0 default.
            - **Explicit ``None``**: secret resolution is disabled and
              every ``${scheme:value}`` passes through verbatim. Internally
              this is mapped to :class:`NullResolver` so the resolution
              path stays uniform; semantically the two are equivalent.
            - **Any** :class:`SecretResolver` **instance** (including
              :class:`NullResolver` directly): used as-is.

    Returns:
        An instance of the schema's target type.

    Raises:
        ValidationFailed: If the final dict fails schema validation.
        SourceUnavailable / SourceParseFailed: From a misbehaving source.
        SecretRefMalformed / SecretSchemeUnsupported / SecretRefUnresolved:
            From secret resolution.
    """
    if not sources:
        raise ValidationFailed("load_config requires at least one source")

    merged: dict[str, object] = {}
    for source in sources:
        snapshot = await source.snapshot()
        merged = deep_merge(merged, snapshot)

    effective_resolver: SecretResolver
    if resolver is _DEFAULT_RESOLVER:
        effective_resolver = EnvResolver()
    elif resolver is None:
        effective_resolver = NullResolver()
    else:
        effective_resolver = resolver

    resolved = await resolve_refs(merged, effective_resolver)
    return schema.validate(resolved)
