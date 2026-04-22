# syntax=docker/dockerfile:1.10
# hadolint global ignore=DL3008,DL3013

# ---- build stage ----------------------------------------------------------
# TEMPLATE_USAGE.md §3: replace @sha256:TODO with an actual digest before release.
FROM python:3.13-slim-trixie@sha256:bada92bcc2794014c291cd97ac3604eb907746b9d3f4b4ff5a8a1ffeff40ceff AS build

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    UV_LINK_MODE=copy \
    UV_PYTHON_DOWNLOADS=never

WORKDIR /src

COPY --from=ghcr.io/astral-sh/uv:0.5.11@sha256:0ac957607303916420297a4c9c213bb33fbd3c888f9cd7f4f7273596ebf42b85 /uv /usr/local/bin/uv

COPY python/pyproject.toml python/uv.lock* ./
RUN --mount=type=cache,target=/root/.cache/uv \
    uv sync --frozen --no-dev --no-install-project

COPY python/src ./src
COPY VERSION ../VERSION
RUN --mount=type=cache,target=/root/.cache/uv \
    uv build --wheel --out-dir /wheels

# ---- runtime stage --------------------------------------------------------
FROM python:3.13-slim-trixie@sha256:bada92bcc2794014c291cd97ac3604eb907746b9d3f4b4ff5a8a1ffeff40ceff AS runtime

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1 \
    PIP_DISABLE_PIP_VERSION_CHECK=1

# non-root identity, read-only filesystem friendly
RUN groupadd --system --gid 10001 app \
 && useradd  --system --uid 10001 --gid app --home-dir /app --shell /sbin/nologin app

WORKDIR /app

COPY --from=build /wheels /wheels
RUN python -m pip install --no-deps /wheels/*.whl \
 && rm -rf /wheels

USER 10001:10001

# TEMPLATE_USAGE.md: run as read-only filesystem — `docker run --read-only --tmpfs /tmp`.
# Consumer overrides CMD. This image exposes no ports by default.

HEALTHCHECK --interval=30s --timeout=5s --start-period=5s --retries=3 \
  CMD python -c "import contriwork_config_core; print(contriwork_config_core.__version__)" || exit 1

CMD ["python", "-c", "import contriwork_config_core; print('ok', contriwork_config_core.__version__)"]
