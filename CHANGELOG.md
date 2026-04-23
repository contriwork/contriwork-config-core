# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html).

Each release MUST contain three language sub-sections (`### Python`, `### C#`, `### npm`). The release workflow refuses to publish if any sub-section is missing.

## [Unreleased]

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

[Unreleased]: https://github.com/contriwork/contriwork-config-core/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/contriwork/contriwork-config-core/compare/v0.0.4...v0.1.0
[0.0.4]: https://github.com/contriwork/contriwork-config-core/compare/v0.0.3...v0.0.4
[0.0.3]: https://github.com/contriwork/contriwork-config-core/compare/v0.0.2...v0.0.3
[0.0.2]: https://github.com/contriwork/contriwork-config-core/compare/v0.0.1...v0.0.2
[0.0.1]: https://github.com/contriwork/contriwork-config-core/compare/v0.0.0...v0.0.1
