"""Secret-ref parsing and walking."""

from __future__ import annotations

import pytest

from contriwork_config_core import SecretRefMalformed
from contriwork_config_core._refs import resolve_refs


class FakeResolver:
    def __init__(self, table: dict[tuple[str, str], str]) -> None:
        self._table = table

    async def resolve(self, scheme: str, value: str) -> str:
        return self._table[(scheme, value)]


async def test_none_resolver_passes_through() -> None:
    data = {"x": "${env:FOO}"}
    assert await resolve_refs(data, None) == data


async def test_single_ref_in_string() -> None:
    resolver = FakeResolver({("env", "FOO"): "bar"})
    assert await resolve_refs("${env:FOO}", resolver) == "bar"


async def test_multiple_refs_in_one_string() -> None:
    resolver = FakeResolver({("env", "USER"): "alice", ("env", "HOST"): "h.example"})
    assert await resolve_refs("${env:USER}@${env:HOST}", resolver) == "alice@h.example"


async def test_ref_with_surrounding_text() -> None:
    resolver = FakeResolver({("env", "PORT"): "5432"})
    assert await resolve_refs("tcp://db:${env:PORT}/app", resolver) == "tcp://db:5432/app"


async def test_walk_nested_dict() -> None:
    resolver = FakeResolver({("env", "URL"): "sqlite:///app.db"})
    data = {"db": {"url": "${env:URL}"}, "debug": False}
    resolved = await resolve_refs(data, resolver)
    assert resolved == {"db": {"url": "sqlite:///app.db"}, "debug": False}


async def test_walk_lists() -> None:
    resolver = FakeResolver({("env", "A"): "1", ("env", "B"): "2"})
    data = {"items": ["${env:A}", "plain", "${env:B}"]}
    resolved = await resolve_refs(data, resolver)
    assert resolved == {"items": ["1", "plain", "2"]}


async def test_double_brace_escape() -> None:
    resolver = FakeResolver({})
    assert await resolve_refs("$${env:FOO}", resolver) == "${env:FOO}"


async def test_escape_does_not_get_resolved() -> None:
    resolver = FakeResolver({("env", "X"): "should-not-appear"})
    assert await resolve_refs("$${env:X}", resolver) == "${env:X}"


async def test_malformed_unclosed_brace() -> None:
    resolver = FakeResolver({})
    with pytest.raises(SecretRefMalformed):
        await resolve_refs("${env:FOO", resolver)


async def test_malformed_no_colon() -> None:
    resolver = FakeResolver({})
    with pytest.raises(SecretRefMalformed):
        await resolve_refs("${env_FOO}", resolver)


async def test_malformed_bad_scheme() -> None:
    resolver = FakeResolver({})
    # Scheme must start with lowercase letter.
    with pytest.raises(SecretRefMalformed):
        await resolve_refs("${ENV:FOO}", resolver)


async def test_number_passes_through_unchanged() -> None:
    resolver = FakeResolver({})
    assert await resolve_refs({"n": 42}, resolver) == {"n": 42}


async def test_bool_passes_through_unchanged() -> None:
    resolver = FakeResolver({})
    assert await resolve_refs({"b": True}, resolver) == {"b": True}
