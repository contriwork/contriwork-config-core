# contriwork-PACKAGE_NAME (Python)

Python adapter for the ContriWork PACKAGE_NAME port. See the [root README](../README.md) and [`CONTRACT.md`](../CONTRACT.md) for the cross-language specification.

## Install

```bash
pip install contriwork-PACKAGE_NAME
```

Requires Python ≥ 3.13.

## Quick start

```python
from contriwork_PACKAGE_NAME import PackageNamePort
# TODO: example once the port has real methods
```

## Local development

```bash
uv sync --all-extras
uv run pytest
uv run ruff check
uv run mypy src
```
