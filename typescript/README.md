# @contriwork/config-core (npm)

Node.js / TypeScript adapter for the ContriWork **ConfigCore** port.
One API surface, three languages (Python / .NET / npm) — this package
is the Node.js implementation.

Cross-language specification, contract, and release history live in the
[GitHub repository](https://github.com/contriwork/contriwork-config-core):

- [Root README](https://github.com/contriwork/contriwork-config-core/blob/main/README.md)
- [`CONTRACT.md`](https://github.com/contriwork/contriwork-config-core/blob/main/CONTRACT.md)
- [`CHANGELOG.md`](https://github.com/contriwork/contriwork-config-core/blob/main/CHANGELOG.md)

Sister packages: [`contriwork-config-core`](https://pypi.org/project/contriwork-config-core/) (PyPI), [`Contriwork.ConfigCore`](https://www.nuget.org/packages/Contriwork.ConfigCore) (NuGet).

## Install

```bash
npm install @contriwork/config-core
# or: pnpm add @contriwork/config-core
# or: yarn add @contriwork/config-core
```

Requires **Node.js ≥ 24**. Dual-published ESM + CJS with bundled
`.d.ts` / `.d.cts` type declarations. Published with
[npm provenance](https://docs.npmjs.com/generating-provenance-statements)
via GitHub Actions OIDC.

## Quick start

```ts
import type { ConfigCorePort } from "@contriwork/config-core";

// TODO: one-line example once the port has real methods.
```

## Local development

```bash
pnpm install --frozen-lockfile
pnpm test
pnpm typecheck
pnpm lint
pnpm build
```

## License

MIT — see [LICENSE](https://github.com/contriwork/contriwork-config-core/blob/main/LICENSE).
