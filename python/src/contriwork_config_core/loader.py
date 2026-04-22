"""Public entry point: ``load_config``.

Order of operations (CONTRACT.md §Behavior):
1. ``Source.snapshot()`` for each source, in declared order.
2. Deep-merge; later sources override earlier.
3. Walk string leaves and resolve ``${scheme:value}`` via the resolver.
4. Validate the merged, resolved dict against the schema.
"""

from __future__ import annotations

from collections.abc import Sequence

from ._merge import deep_merge
from ._refs import resolve_refs
from .errors import ValidationFailed
from .resolvers import EnvResolver, SecretResolver
from .schema import SchemaAdapter
from .sources import Source


class _DefaultResolverSentinel:
    """Marker for ``resolver=<omitted>``; distinguishable from explicit ``None``."""


_DEFAULT_RESOLVER = _DefaultResolverSentinel()


async def load_config[T](
    schema: SchemaAdapter[T],
    sources: Sequence[Source],
    resolver: SecretResolver | None | _DefaultResolverSentinel = _DEFAULT_RESOLVER,
) -> T:
    """Load, merge, resolve, and validate.

    Args:
        schema: A :class:`SchemaAdapter` that validates the merged dict.
        sources: Ordered list of :class:`Source` instances. Later sources
            override earlier ones. Empty list is an error.
        resolver: Optional :class:`SecretResolver` for ``${...}`` refs.
            - If omitted, defaults to :class:`EnvResolver` (env-only).
            - Pass ``None`` to disable secret resolution entirely (refs
              pass through as literal strings).

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

    effective_resolver: SecretResolver | None
    if isinstance(resolver, _DefaultResolverSentinel):
        effective_resolver = EnvResolver()
    else:
        effective_resolver = resolver

    resolved = await resolve_refs(merged, effective_resolver)
    return schema.validate(resolved)
