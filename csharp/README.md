# Contriwork.ConfigCore (.NET)

.NET adapter for the ContriWork **ConfigCore** port. One API surface,
three languages (Python / .NET / npm) — this package is the .NET
implementation.

Cross-language specification, contract, and release history live in the
[GitHub repository](https://github.com/contriwork/contriwork-config-core):

- [Root README](https://github.com/contriwork/contriwork-config-core/blob/main/README.md) — ecosystem overview
- [`CONTRACT.md`](https://github.com/contriwork/contriwork-config-core/blob/main/CONTRACT.md) — language-agnostic port spec
- [`CHANGELOG.md`](https://github.com/contriwork/contriwork-config-core/blob/main/CHANGELOG.md)

Sister packages: [`contriwork-config-core`](https://pypi.org/project/contriwork-config-core/) (PyPI), [`@contriwork/config-core`](https://www.npmjs.com/package/@contriwork/config-core) (npm).

## Install

```bash
dotnet add package Contriwork.ConfigCore
```

Targets **.NET 10 LTS**.

## Quick start

```csharp
using Contriwork.ConfigCore;

// TODO: one-line example once the port has real methods.
```

## Local development

```bash
dotnet restore
dotnet build
dotnet test
dotnet format --verify-no-changes
```

## License

MIT — see [LICENSE](https://github.com/contriwork/contriwork-config-core/blob/main/LICENSE).
