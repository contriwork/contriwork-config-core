# contriwork-config-core (Python)

Python adapter for the ContriWork **config-core** port. One API surface,
three languages (Python / .NET / npm) — this package is the Python
implementation.

Cross-language specification, contract, and release history live in the
[GitHub repository](https://github.com/contriwork/contriwork-config-core):

- [Root README](https://github.com/contriwork/contriwork-config-core/blob/main/README.md) — ecosystem overview
- [`CONTRACT.md`](https://github.com/contriwork/contriwork-config-core/blob/main/CONTRACT.md) — language-agnostic port spec
- [`CHANGELOG.md`](https://github.com/contriwork/contriwork-config-core/blob/main/CHANGELOG.md)

Sister packages: [`Contriwork.ConfigCore`](https://www.nuget.org/packages/Contriwork.ConfigCore) (NuGet), [`@contriwork/config-core`](https://www.npmjs.com/package/@contriwork/config-core) (npm).

## Install

```bash
pip install contriwork-config-core
```

Requires **Python ≥ 3.13**.

## Quick start

```python
import asyncio

from pydantic import BaseModel, SecretStr

from contriwork_config_core import (
    EnvSource,
    FileSource,
    PydanticAdapter,
    load_config,
)


class AppConfig(BaseModel):
    db_url: str
    debug: bool = False
    api_key: SecretStr | None = None


async def main() -> AppConfig:
    return await load_config(
        schema=PydanticAdapter(AppConfig),
        sources=[
            FileSource("./config.yaml", required=False),
            FileSource("./.env", required=False),  # v0.2.0: native dotenv
            EnvSource(prefix="APP_"),
        ],
    )


cfg = asyncio.run(main())
```

`load_config` is `async`. Calling it without `await` returns a coroutine
object — not a config — and `mypy` / `pyright` will flag the call site.
See [`CONTRACT.md`](https://github.com/contriwork/contriwork-config-core/blob/main/CONTRACT.md)
for the full operation order (snapshot → merge → resolve → validate) and
the error taxonomy.

### Bootstrapping inside an async server

The naive bootstrap pattern — calling `await load_config(...)` from inside
an `async def` lifespan handler with a sync wrapper at module-import time
— fails non-obviously under hot-reload-capable web servers
(`uvicorn --reload`, `hypercorn --reload`, etc.). The reload subprocess
imports the user app **from inside an already-running event loop**, so
any module-import-time code that wraps `asyncio.run(load_config(...))`
raises `RuntimeError: asyncio.run() cannot be called from a running event
loop`. The error is silent in the sense that the bootstrap is skipped,
the cached config singleton stays `None`, and the failure surfaces later
as a confusing runtime error in unrelated code.

The naive `try / except get_running_loop()` workaround correctly detects
the running loop but has no fallback path. The pattern below isolates
the load to a fresh thread — its own event loop — so `asyncio.run()`
succeeds whether or not the importing thread already has a loop:

```python
import asyncio
import threading
from collections.abc import Callable, Coroutine
from typing import TypeVar

T = TypeVar("T")


def run_async_blocking(coro_factory: Callable[[], Coroutine[object, object, T]]) -> T:
    """Run an async loader synchronously, even when called from inside a
    running event loop.

    Why: ``uvicorn --reload`` imports the app from inside its own uvloop
    event loop. ``asyncio.run()`` refuses to nest, so we delegate to a
    fresh thread that owns a brand-new loop.
    """
    try:
        asyncio.get_running_loop()
        has_running_loop = True
    except RuntimeError:
        has_running_loop = False

    if not has_running_loop:
        return asyncio.run(coro_factory())  # normal path

    result: list[T] = []
    error: list[BaseException] = []

    def _runner() -> None:
        try:
            result.append(asyncio.run(coro_factory()))
        except BaseException as e:  # noqa: BLE001 — re-raised on the caller side
            error.append(e)

    t = threading.Thread(target=_runner, daemon=True)
    t.start()
    t.join()

    if error:
        raise error[0]
    return result[0]


# Module-level singleton — works under uvicorn --reload and in plain scripts.
_CONFIG = run_async_blocking(
    lambda: load_config(
        schema=PydanticAdapter(AppConfig),
        sources=[FileSource("./config.yaml"), EnvSource(prefix="APP_")],
    )
)
```

This helper lives in caller code on purpose — every app's bootstrap is
slightly different (telemetry, fallback config, lazy-init policy) and
shipping a one-size-fits-all utility inside `contriwork-config-core` would
just push the same trap one layer deeper. The async API is the contract;
the threading fallback is caller policy.

## Local development

```bash
uv sync --all-extras
uv run pytest
uv run ruff check
uv run mypy src
```

## License

MIT — see [LICENSE](https://github.com/contriwork/contriwork-config-core/blob/main/LICENSE).
