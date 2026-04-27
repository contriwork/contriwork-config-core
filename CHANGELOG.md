# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html).

Each release MUST contain three language sub-sections (`### Python`, `### C#`, `### npm`). The release workflow refuses to publish if any sub-section is missing.

## [Unreleased]

## [0.2.0] — 2026-04-27

Six items distilled from the real-world Digital Worker integration of
v0.1.0. Contract revision stays at **v1** — every change in this release
is additive and the existing v1 surface is preserved exactly. See
[`docs/v0.2.0-backlog.md`](./docs/v0.2.0-backlog.md) for the per-item
Context / Problem / Proposal framing.

Headline additions:

- `EnvSource(decode_json_for=...)` — opt-in JSON decode for env values
  whose JSON-decoded category matches one of `"list" / "dict" / "bool" /
  "int" / "float"`. Default of empty preserves the v0.1.0 raw-string
  behaviour exactly.
- `FileSource(format="dotenv")` — native `.env` parsing (KEY=VALUE,
  comments, blank lines, quoting, `export` prefix). `.env` extension is
  auto-inferred.
- `secret_str_or_empty` / `secret_str_required` (Python) and the parity
  helpers in C# / TS — centralize the SecretStr unwrap pattern.
- `NullResolver` — a named `SecretResolver` for the explicit opt-out
  path; `load_config(resolver=None)` is mapped to `NullResolver()`
  internally so callers and contract are uniformly aligned.

Per-language details below.

### Python

- Added `EnvSource(decode_json_for=...)` opt-in JSON decode. Categories:
  `"list"`, `"dict"`, `"bool"`, `"int"`, `"float"`. Decode is best-effort
  — unparseable values pass through as the raw string, parsed values
  whose category isn't enabled also pass through.
- Added `FileSource(format="dotenv")` native dotenv parsing; `.env`
  extension auto-inferred. Result is a flat dict with verbatim keys.
- Added `secret_str_or_empty(value)` and
  `secret_str_required(value, field_name)` helpers in the public
  namespace. Both raise `TypeError` when handed a non-`SecretStr` input
  to surface schema bugs early instead of silently coercing a plain `str`.
- Added `NullResolver` to the public namespace. Maps the
  `load_config(resolver=None)` opt-out to a named class internally so
  the resolution path stays uniform.
- `load_config` parameter signature now reads as the honest
  `resolver: SecretResolver | None` in `inspect.signature` and IDE hover
  (the `_DefaultResolverSentinel` is hidden via `typing.cast`). Public
  behaviour is unchanged: omitting `resolver` still defaults to
  `EnvResolver()`; explicit `None` still disables resolution.
- Added regression test that `inspect.iscoroutinefunction(load_config)`
  is `True` and the public parameter list is stable.
- README: replaced the placeholder Quick start with a working FileSource
  + EnvSource + PydanticAdapter example, and added a "Bootstrapping
  inside an async server" section that documents the `uvicorn --reload`
  trap and ships a runnable thread-isolated `run_async_blocking` helper.

### C#

- Added `JsonCategory` enum (`List` / `Dict` / `Bool` / `Int` / `Float`)
  and the `decodeJsonFor` constructor parameter on `EnvSource`. Default
  preserves the v0.1.0 string-only behaviour. Enum members are kept
  identical to the cross-language string values for contract-fixture
  parity (with an inline `CA1720` suppression).
- Added `FileFormat.Dotenv` and the `.env` extension inference in
  `FileSource`. Dotenv parsing is the same flat-verbatim subset as
  Python and TypeScript.
- Added `Secrets.SecretStrOrEmpty(string?)` and
  `Secrets.SecretStrRequired(string?, string fieldName)` helpers as a
  parity surface for the Python `secret_str_*` family.
- Added `NullResolver : ISecretResolver`. `ConfigLoader.LoadConfigAsync`
  maps an explicit `null` resolver argument to a `NullResolver` instance
  internally.

### npm

- Added `JsonCategory` type union (`"list" | "dict" | "bool" | "int" |
  "float"`) and the `decodeJsonFor` option on `EnvSourceOptions`. Default
  preserves v0.1.0 behaviour.
- Added `"dotenv"` to the `FileFormat` union and `.env` extension
  inference in `FileSource`. Same flat-verbatim subset as Python and C#.
- Added `secretStrOrEmpty(value)` and `secretStrRequired(value, fieldName)`
  helpers as a parity surface for the Python `secret_str_*` family.
  Input type is `string | null | undefined`.
- Added `NullResolver` class implementing `SecretResolver`. `loadConfig`
  maps an explicit `resolver: null` argument to a `NullResolver` instance
  internally; passing `NullResolver` directly is the recommended named
  opt-out.

## [0.1.0] — 2026-04-22

First **behaviour-bearing** release. Contract revision bumps v0 → v1; the
scaffolds shipped in 0.0.1 – 0.0.4 become functional libraries.

Accumulated scope since 0.0.4 (PRs #21 – #27):

- Contract v1 specified (PR #23): `load_config` / `LoadConfigAsync` /
  `loadConfig` + `Source` and `SecretResolver` protocols + `SchemaAdapter`
  interface + six-entry error taxonomy + `${scheme:value}` secret refs
  with `$${…}` escape + strict merge rules (first source = lowest
  precedence; deep-merge dicts; replace lists and scalars).
- Per-language implementations of the full v1 surface (PRs #25 / #26 /
  #27). Identical shape across Python / .NET / TypeScript modulo
  language-idiomatic casing.
- Housekeeping: manifest descriptions (PR #24), contract-doc polish
  (PR #21), release tooling (`scripts/release.sh` + `docs/RELEASE.md`,
  PR #22) — none of which are in package tarballs but cumulatively clean
  up consumer-facing surface (registry descriptions, linked GitHub
  docs).

Out of scope for 0.1.0 (explicitly deferred):

- External secret managers (Vault, AWS Secrets Manager, Azure Key Vault,
  GCP Secret Manager, Doppler, 1Password) — they will ship as separate
  adapter packages that implement `Source` and/or `SecretResolver`.
- Runtime-mutable / UI-editable settings — that's an application concern
  (Digital Worker uses its own `AppSetting` table); config-core stops at
  bootstrap-time static config.
- Hot reload. Reload = call the loader again.
- `contract-tests/test_cases.json` fixture fill-in. The runners still
  skip in all three languages; fixture format is its own design task.

### Python

- Added `load_config(schema, sources, resolver?)` as the async entry
  point.
- Added built-in sources: `EnvSource(prefix, separator)`,
  `FileSource(path, format?, required?)` (yaml/json/toml with
  extension-inferred format), `InMemorySource`.
- Added built-in resolvers: `EnvResolver`, `FileResolver(base_dir?)`,
  `ChainResolver(*resolvers)`.
- Added `PydanticAdapter(BaseModel)` as the default schema adapter;
  `SchemaAdapter` Protocol is pluggable.
- Added `ConfigError` hierarchy (6 classes matching CONTRACT codes).
- New runtime deps: `pydantic>=2.6`, `pyyaml>=6.0`.
- Removed v0 `ConfigCorePort` Protocol placeholder; function-first API.

### C#

- Added `ConfigLoader.LoadConfigAsync<T>(schema, sources, resolver?)`
  plus a convenience overload that defaults to `EnvResolver`.
- Added `ISource` implementations: `EnvSource`, `FileSource`
  (yaml/json/toml via YamlDotNet + Tomlyn + System.Text.Json),
  `InMemorySource`.
- Added `ISecretResolver` implementations: `EnvResolver`, `FileResolver`,
  `ChainResolver`.
- Added `ISchemaAdapter<T>` + `DataAnnotationsAdapter<T>` that wraps
  `Validator.TryValidateObject`. Applies `JsonNamingPolicy.SnakeCaseLower`
  and a lenient bool converter so env-supplied strings ("true", "42")
  hydrate into `bool` / `int` properties.
- Added `ConfigException` abstract base with six sealed subclasses
  matching CONTRACT codes.
- Package deps: `YamlDotNet 16.3.0`, `Tomlyn 0.19.0`.
- Removed v0 `IConfigCorePort` interface placeholder.

### npm

- Added `loadConfig({ schema, sources, resolver? })` as the async entry
  point (options-bag signature).
- Added source classes: `EnvSource`, `FileSource` (yaml/json/toml),
  `InMemorySource`.
- Added resolver classes: `EnvResolver`, `FileResolver`, `ChainResolver`.
- Added `ZodAdapter<S>` as the default schema adapter; `SchemaAdapter<T>`
  is one-method-wide so valibot / arktype / ajv adapters are one file.
- Added `ConfigError` class hierarchy matching CONTRACT codes.
- New runtime deps: `zod`, `yaml`, `smol-toml`.
- Removed v0 `ConfigCorePort` type placeholder.

## [0.0.4] — 2026-04-22

Docs-only patch: ship per-registry READMEs so each package page displays content tailored to its registry's consumer. Backport of template commit `62ff29c`. No code changes; supported runtimes identical to 0.0.3.

### Python

- docs: per-registry README replaces generic placeholder; absolute GitHub URLs for cross-language links (relative paths broke on PyPI).

### C#

- docs: new C#-specific README ships to NuGet; csproj no longer packs the repo-root README (which contained cross-registry badges and Python/npm references).

### npm

- docs: new npm-specific README ships to npmjs.com (previous release had "no README" on the package page).

## [0.0.3] — 2026-04-22

Second retry of the infrastructure smoke test. `0.0.2` fixed the NuGet username casing but the wildcard trust policy on nuget.org remained in the 7-day dormant "Use within N days to keep it permanently active" state and silently rejected the OIDC claim even after `Activate for 7 days`. Replaced it with a package-specific policy (`Repository: contriwork-config-core` instead of `*`, marked **Active**). No code change from 0.0.2; this release exists to drive the first successful NuGet publish.

### Python

- Republish as `contriwork-config-core==0.0.3` (identical to 0.0.2).

### C#

- First successful publish on NuGet as `Contriwork.ConfigCore@0.0.3`. 0.0.1 and 0.0.2 were never published to NuGet (see notes on each).

### npm

- Republish as `@contriwork/config-core@0.0.3` (identical to 0.0.2); `latest` dist-tag moves to 0.0.3.

## [0.0.2] — 2026-04-22

Re-release intended to fix `0.0.1`'s NuGet failure. The original 401 error reported `user 'contriwork'` in lowercase; the NuGet account is registered as `ContriWork` (PascalCase), so we pinned `user: ContriWork` in `release-dotnet.yml`. NuGet still rejected the claim — the real blocker turned out to be the wildcard trust policy's dormant grace-period state, which was only surfaced once the case fix ruled out the earlier hypothesis. Superseded by 0.0.3.

### Python

- Republish as `contriwork-config-core==0.0.2` (identical to 0.0.1).

### C#

- Attempted publish of `Contriwork.ConfigCore@0.0.2`; failed on the OIDC token exchange (same 401 as 0.0.1, different root cause). Superseded by 0.0.3.

### npm

- Republish as `@contriwork/config-core@0.0.2` (identical to 0.0.1); `latest` dist-tag moves to 0.0.2.

## [0.0.1] — 2026-04-22

Infrastructure smoke-test release. Publishes the scaffold to PyPI, NuGet, and npm under the final package identifiers to validate the end-to-end release pipeline. No behavior yet; `ConfigCorePort` / `IConfigCorePort` remain interface-only placeholders for Senaryo B.

### Python

- Initial scaffold release on PyPI as `contriwork-config-core`.

### C#

- Initial scaffold release on NuGet as `Contriwork.ConfigCore`.

### npm

- Initial scaffold release on npm as `@contriwork/config-core`. The `0.0.0` placeholder previously published under the `pending` dist-tag is superseded; `0.0.1` becomes `latest`.

[Unreleased]: https://github.com/contriwork/contriwork-config-core/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/contriwork/contriwork-config-core/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/contriwork/contriwork-config-core/compare/v0.0.4...v0.1.0
[0.0.4]: https://github.com/contriwork/contriwork-config-core/compare/v0.0.3...v0.0.4
[0.0.3]: https://github.com/contriwork/contriwork-config-core/compare/v0.0.2...v0.0.3
[0.0.2]: https://github.com/contriwork/contriwork-config-core/compare/v0.0.1...v0.0.2
[0.0.1]: https://github.com/contriwork/contriwork-config-core/compare/v0.0.0...v0.0.1
