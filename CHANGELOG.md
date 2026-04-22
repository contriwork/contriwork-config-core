# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html).

Each release MUST contain three language sub-sections (`### Python`, `### C#`, `### npm`). The release workflow refuses to publish if any sub-section is missing.

## [Unreleased]

## [0.0.2] — 2026-04-22

Re-release of the infrastructure smoke test. `0.0.1` shipped on PyPI and npm but failed on NuGet with `Token exchange failed (401): No matching trust policy owned by user 'contriwork' was found` — the NuGet account is registered as `ContriWork` (PascalCase) and the trust-policy lookup is case-sensitive, so the lowercase `user: contriwork` in `release-dotnet.yml` never matched. Fixed by pinning `user: ContriWork`. No behaviour change.

### Python

- Republish as `contriwork-config-core==0.0.2` (identical to 0.0.1).

### C#

- First successful publish on NuGet as `Contriwork.ConfigCore@0.0.2`. 0.0.1 was never published to NuGet.

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

[Unreleased]: https://github.com/contriwork/contriwork-config-core/compare/v0.0.2...HEAD
[0.0.2]: https://github.com/contriwork/contriwork-config-core/compare/v0.0.1...v0.0.2
[0.0.1]: https://github.com/contriwork/contriwork-config-core/compare/v0.0.0...v0.0.1
