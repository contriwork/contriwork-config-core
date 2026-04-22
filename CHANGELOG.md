# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html).

Each release MUST contain three language sub-sections (`### Python`, `### C#`, `### npm`). The release workflow refuses to publish if any sub-section is missing.

## [Unreleased]

## [0.0.1] — 2026-04-22

Infrastructure smoke-test release. Publishes the scaffold to PyPI, NuGet, and npm under the final package identifiers to validate the end-to-end release pipeline. No behavior yet; `ConfigCorePort` / `IConfigCorePort` remain interface-only placeholders for Senaryo B.

### Python

- Initial scaffold release on PyPI as `contriwork-config-core`.

### C#

- Initial scaffold release on NuGet as `Contriwork.ConfigCore`.

### npm

- Initial scaffold release on npm as `@contriwork/config-core`. The `0.0.0` placeholder previously published under the `pending` dist-tag is superseded; `0.0.1` becomes `latest`.

[Unreleased]: https://github.com/contriwork/contriwork-config-core/compare/v0.0.1...HEAD
[0.0.1]: https://github.com/contriwork/contriwork-config-core/compare/v0.0.0...v0.0.1
