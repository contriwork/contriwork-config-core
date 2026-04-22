# CONTRACT — ConfigCore

This document is the **language-agnostic contract** for this package. It is the single source of truth for the public surface. Every change to public behavior MUST start here before any code is written in `python/`, `csharp/`, or `typescript/`.

Contract revision (bumped on any behavior-visible change): **v1**

---

## Overview

`ConfigCore` loads, merges, and validates static application configuration from multiple ordered sources, resolves `${scheme:ref}` secret references inline, and returns a typed, validated config object to the caller. It is cross-language: a Python, .NET, and TypeScript implementation ship together at the same contract revision, with identical operations, identical error codes, and identical precedence rules so that a Python service and a TypeScript frontend can point at the same YAML file with the same result.

Scope is **bootstrap-time, static configuration** (environment variables, files on disk, secret stores). Runtime-mutable, user-editable settings — the kind that would back an admin UI — are **out of scope**; they belong in a DB-backed store that the application owns. `ConfigCore` loads what the app needs to come up, and returns; it does not maintain state, watch for changes, or provide a hot-reload primitive. Reloading is the caller's responsibility: call the loader again.

---

## Port (language-agnostic interface)

The port defines the operations exposed across all three language implementations. Method names MUST be identical modulo language-idiomatic casing (`snake_case` for Python, `PascalCaseAsync` for C#, `camelCase` for TypeScript).

### Core loader

| Operation | Input | Output | Failure modes |
|-----------|-------|--------|---------------|
| `load_config` | `schema`, `sources: Source[]`, `resolver?: SecretResolver` | `T` (instance of the schema type) | `VALIDATION_FAILED`, `SOURCE_UNAVAILABLE`, `SOURCE_PARSE_FAILED`, `SECRET_REF_UNRESOLVED`, `SECRET_REF_MALFORMED`, `SECRET_SCHEME_UNSUPPORTED` |

### `Source` protocol

A `Source` exposes a snapshot of configuration keys. Implementations provided in the core package:

- `EnvSource(prefix?: string, separator?: string = "__")` — reads from the process environment. `prefix + "APP__DB__URL"` maps to nested `db.url` via `separator`.
- `FileSource(path: string, format?: "yaml" | "json" | "toml")` — reads a file; format inferred from extension if omitted.
- `InMemorySource(data: dict)` — primarily for tests.

Adapters for external secret managers (Vault, AWS Secrets Manager, Azure Key Vault, GCP Secret Manager, Doppler, 1Password CLI) ship as **separate packages** that implement `Source` and/or `SecretResolver`. They are not part of `ConfigCore` v1.

| Operation | Input | Output | Failure modes |
|-----------|-------|--------|---------------|
| `Source.snapshot` | — | `dict[str, Any]` (nested) | `SOURCE_UNAVAILABLE`, `SOURCE_PARSE_FAILED` |

### `SecretResolver` protocol

Resolves secret references (`${scheme:value}`) found inside string leaves of the merged config dict. The core package ships with:

- `EnvResolver` — resolves `${env:NAME}` from the process environment.
- `FileResolver(base_dir?: string)` — resolves `${file:/abs/path}` or `${file:relative}` (relative to `base_dir`) by reading the file contents, stripped of trailing whitespace.
- `ChainResolver(*resolvers)` — tries each resolver in order; first one that claims the scheme wins.

External schemes (`${vault:…}`, `${aws:…}`, etc.) are provided by adapter packages that register additional `SecretResolver` instances.

| Operation | Input | Output | Failure modes |
|-----------|-------|--------|---------------|
| `SecretResolver.resolve` | `scheme: string, value: string` | `string` (resolved secret) | `SECRET_REF_UNRESOLVED`, `SECRET_SCHEME_UNSUPPORTED` |

---

## Methods

### `load_config`

- **Canonical signature:** `load_config(schema: SchemaAdapter[T], sources: Source[], resolver: SecretResolver | None = None) -> T`
  - **Python:** `await load_config(schema=PydanticModel, sources=[...], resolver=EnvResolver())`
  - **C#:** `await ConfigBuilder.For<TConfig>().AddFile(...).AddEnv().BuildAsync(resolver?)`
  - **TypeScript:** `await loadConfig({ schema: zodSchema, sources: [...], resolver? })`

- **Parameters:**
  - `schema` — a language-idiomatic type adapter: pydantic `BaseModel` class in Python, a POCO + `DataAnnotations` (or custom validator) in C#, a `zod` / `valibot` / `arktype` schema object in TypeScript. The package does not bundle a schema library — it consumes one via an adapter interface.
  - `sources` — ordered list of `Source` instances. Order is precedence: **later sources override earlier sources**. Empty list is an error (`VALIDATION_FAILED` — nothing to load).
  - `resolver` — optional. If omitted, defaults to `EnvResolver()` (env-only secret resolution). Passing an explicit `None` / `null` disables secret resolution entirely (any `${…}` string passes through unchanged).

- **Returns:** a fully-validated instance of the schema type. For Python pydantic, an instance of the `BaseModel` subclass. For C#, an instance of `TConfig`. For TypeScript, the `z.infer<typeof schema>` type.

- **Preconditions:**
  - Every source's `snapshot()` returns valid structured data.
  - The schema is well-formed for its language's validator.
  - At least one source is provided.

- **Postconditions:**
  - The returned value has been validated against the schema.
  - All `${scheme:ref}` strings inside the merged dict have been resolved (or raised).
  - No reference to the source objects is retained — the loader does not hold a watcher.

- **Operation order** (strict):
  1. For each source in declared order, call `snapshot()`. A source's snapshot may arrive as nested dicts or flat dotted keys; flat keys are expanded to nested via the source's `separator`.
  2. Deep-merge snapshots: **later sources override earlier**. Dicts merge key-wise; lists and scalars replace.
  3. Walk the merged dict, resolving `${scheme:ref}` in string leaves via the `SecretResolver`. Only string leaves are scanned; a literal `${env:…}` inside a number or a list is a bug in the source, not a feature to interpret.
  4. Validate the resolved dict against the schema. Return the typed instance.

- **Idempotency:** Calling `load_config` twice with the same `sources` and `resolver` (whose backing state has not changed) MUST produce equal output. This is the caller's way to reload: call again.

### `Source.snapshot`

- **Canonical signature:** `snapshot() -> dict[str, Any]`
- **Returns:** a dict keyed by top-level config names, nested for structured values. Keys are lowercase snake_case by convention; source implementations MAY normalize (e.g., `EnvSource` lowercases).
- **Preconditions:** none.
- **Postconditions:**
  - `snapshot()` is **pure with respect to side effects visible to the caller**: no writes, no logs above DEBUG.
  - `snapshot()` is **not required to be pure with respect to the environment**: `EnvSource.snapshot()` reads `os.environ` live each call.

### `SecretResolver.resolve`

- **Canonical signature:** `resolve(scheme: str, value: str) -> str`
- **Parameters:**
  - `scheme` — the portion before the colon in `${scheme:value}`, normalized to lowercase.
  - `value` — the portion after the colon, verbatim.
- **Returns:** the resolved secret as a string.
- **Preconditions:** the caller has already parsed `${…}` syntax and extracted (scheme, value). Malformed refs never reach `resolve`; they are the caller's responsibility.
- **Postconditions:** the returned string is safe to substitute into the config dict. No trailing whitespace from file reads.

---

## Behavior

### Precedence and merging

- Source order is **first to last = lowest to highest precedence**. Intuition: put platform defaults (a checked-in YAML) first, environment-specific overrides last.
- Merging is **deep** for dicts, **replace** for lists and scalars. This matches the most common config semantics (.NET `ConfigurationBuilder`, Pydantic Settings, `viper`). Merging is NOT a set union for lists — a later source's `["a", "b"]` fully replaces an earlier `["a", "b", "c"]`.

### Secret ref syntax

- Syntax: `${scheme:value}` — exactly one pair of braces, a scheme (lowercase alphanumeric + `_`), a literal `:`, and a value (any char except `}`).
- A string leaf MAY contain multiple refs interleaved with literal text: `${env:USER}@${env:HOST}` resolves each independently.
- Unknown schemes raise `SECRET_SCHEME_UNSUPPORTED`. Malformed syntax (unclosed brace, missing colon) raises `SECRET_REF_MALFORMED`.
- If the caller wants a literal `${…}` in a config value (no interpolation), they escape it with a doubled brace: `$${literal}`. This is the only escape; source formats' own quoting rules apply before config-core sees the string.

### Idempotency

- Given unchanging sources and resolver, `load_config` is a pure function.
- Re-calling `load_config` to pick up a changed env variable or a rewritten file is supported and documented. There is no cached load, no singleton.

### Side effects

- No logs above DEBUG level from the core package.
- No writes to disk, no network calls from the core package. `FileSource` reads the file passed in; adapters may make network calls (documented per adapter).
- No ambient environment mutation — `load_config` does not set or unset env vars.

### Ordering / concurrency

- `load_config` is re-entrant: multiple concurrent calls with independent arguments are safe. Sources are called sequentially within one `load_config` call; parallelization is not guaranteed.
- Sources and resolvers MAY be called concurrently across separate `load_config` calls; their own thread-safety is an implementation-per-source concern.

### Resource ownership

- The caller owns all source objects; `load_config` does not close or dispose anything.
- For `FileSource`, the file is opened, read in full, and closed before `snapshot()` returns.

---

## Error Taxonomy

Every failure mode has a **stable error code** (SCREAMING_SNAKE_CASE), a language-agnostic description, and a per-language exception type that wraps it (`ConfigError` base class; subclasses per code).

| Code | Description | When it is raised |
|------|-------------|-------------------|
| `VALIDATION_FAILED` | The merged, resolved dict did not validate against the schema. | After secret resolution, if the schema validator rejects the input. The raised error wraps the validator's own diagnostic (pydantic `ValidationError`, .NET `ValidationResult[]`, zod `ZodError`). |
| `SOURCE_UNAVAILABLE` | A declared source could not be opened or read. | `FileSource` with a missing file when `required=True` (default). `EnvSource` never raises this — a missing env var yields no key. |
| `SOURCE_PARSE_FAILED` | A source's content could not be parsed into a dict. | `FileSource` on malformed YAML/JSON/TOML. |
| `SECRET_REF_MALFORMED` | A `${…}` reference did not match the syntax. | During the resolution walk, before any resolver is called. |
| `SECRET_SCHEME_UNSUPPORTED` | The reference's scheme has no registered resolver. | During the resolution walk, if no resolver claims the scheme. |
| `SECRET_REF_UNRESOLVED` | A resolver accepted the scheme but could not produce a value. | E.g., `${env:NOT_SET}` with no default; `${file:/nonexistent}`. |

Schema-validator libraries' own errors are not re-thrown as-is — they are wrapped so that the `ConfigError` / `VALIDATION_FAILED` code is stable across validator choices.

---

## Config Schema

`ConfigCore` itself takes no configuration. It is a pure function of its arguments.

Source adapters and secret resolvers are configured via their constructors; the arguments are part of **their** contracts, not this one. See each adapter's documentation.

---

## Invariants

Properties that MUST hold across all releases at the same contract revision (v1):

- `load_config`'s signature does not change shape (arg count, arg meaning) without a contract bump.
- Source precedence (first = lowest, last = highest) is fixed.
- Deep-merge-dicts / replace-lists-and-scalars is fixed.
- Secret ref syntax (`${scheme:value}`, `$${…}` escape) is fixed.
- Error codes are never renamed within v1. New codes MAY be added for new failure modes as long as existing callers' `except ConfigError` / `catch (ConfigException)` handlers still catch them.
- `EnvSource`, `FileSource`, `InMemorySource`, `EnvResolver`, `FileResolver`, `ChainResolver` are guaranteed to exist with the signatures documented here. They MAY grow optional parameters (defaulting to current behavior).
- `EnvSource`'s default separator is `__`. It does not change.
- `FileSource`'s format inference rules (`.yaml`/`.yml` → yaml, `.json` → json, `.toml` → toml) do not change.

---

## Compatibility

- **Python**: ≥ 3.13 (see `VERSION_MATRIX.md`).
- **.NET**: ≥ 10.0 LTS.
- **Node.js**: ≥ 24 Active LTS.
- **npm strategy**: pure-TS (see [`typescript/src/strategy.md`](./typescript/src/strategy.md)).

Runtime baseline is a hard constraint — no parallel matrix support for older LTSes.

### Schema-library coupling

`ConfigCore` does not bundle a schema library. It consumes one via a thin `SchemaAdapter` interface:

| Language | Default adapter | Adapter API (what the adapter must expose) |
|----------|-----------------|---------------------------------------------|
| Python | `PydanticAdapter` wrapping a `BaseModel` class | `validate(data: dict) -> T` (raises `pydantic.ValidationError`) |
| C# | `DataAnnotationsAdapter<T>` | `Validate(dict) -> T` (raises `ValidationException`) |
| TypeScript | `ZodAdapter` (primary), `ValibotAdapter`, `ArkTypeAdapter` (secondary) | `parse(data: unknown) -> T` |

Callers who want a different validator can write their own adapter — it is one method wide. Internal adapter changes are not contract-bumps.

---

## Change Log

Contract revisions ONLY — bumped when any of the sections above change in a way a consumer can observe. Does NOT track patch fixes or internal refactors; those go in `CHANGELOG.md`.

| Revision | Summary | Released with package version |
|----------|---------|-------------------------------|
| v0 | Initial scaffold. `ConfigCorePort` / `IConfigCorePort` as empty type declarations. | 0.0.1 – 0.0.4 |
| v1 | First behaviour-bearing revision: `load_config` operation, `Source` and `SecretResolver` protocols, three built-in sources (env / file / in-memory), three built-in resolvers (env / file / chain), strict merge rules, `${scheme:value}` secret refs, six-entry error taxonomy. Implementations ship as `0.1.0`. | 0.1.0 (pending) |
