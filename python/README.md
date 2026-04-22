# contriwork-config-core (Python)

Python adapter for the ContriWork config-core port. See the [root README](../README.md) and [`CONTRACT.md`](../CONTRACT.md) for the cross-language specification.

## Install

```bash
pip install contriwork-config-core
```

Requires Python ≥ 3.13.

## Quick start

```python
from contriwork_config_core import ConfigCorePort
# TODO: example once the port has real methods
```

## Local development

```bash
uv sync --all-extras
uv run pytest
uv run ruff check
uv run mypy src
```
