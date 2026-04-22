# npm Production Strategy

The ContriWork roadmap (`PACKAGES_ROADMAP.md §3.5`) lists five possible strategies for shipping a package on npm. This file documents which one this package uses and **why**.

## Decision

**Strategy A — pure-TS reimplementation.** Confirmed for the `ConfigCore` port.

## Alternatives considered

| ID  | Name                                           | When to pick                                                                      | Trade-off                                                                           |
| --- | ---------------------------------------------- | --------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------- |
| A   | **Pure-TS reimplementation**                   | Pure-logic package (parsers, validators, encoders, algorithms). Zero native deps. | Must maintain three code lines. Behaviour parity enforced only by `contract-tests`. |
| B   | **WASM** (compile Rust/Go/C++ to WebAssembly)  | Perf-critical core shared between runtimes.                                       | Toolchain complexity; binary size; restricted syscalls.                             |
| C   | **N-API / node-gyp native addon**              | Existing C/C++ codebase, must match bit-for-bit.                                  | Cross-platform build pain; prebuilt-binary hosting; sandbox/security surface.       |
| D   | **Sidecar** (bundled binary / subprocess)      | Large Python/.NET runtime, not worth rewriting.                                   | Process management, startup cost, platform-matrix of binaries.                      |
| E   | **HTTP client** (pointing at a hosted service) | Package wraps a SaaS or centralised service the org runs.                         | Requires infra; not usable offline; introduces network as dependency.               |

## Rationale

`ConfigCore`'s surface is pure configuration logic (parsing, validation, defaults, error mapping) — no native codebase to wrap and no perf hot path that would justify WASM, N-API, sidecar, or HTTP client. Contract parity is enforced by `contract-tests/`, which runs the same fixtures against the Python, .NET, and TypeScript implementations, so the three code lines drift only when someone skips the test suite. The trade-off (three implementations to update for a behaviour change) is acceptable at the contract's current size and grows linearly, not exponentially, as operations are added. Revisit this decision only if a reference implementation in another language emerges whose behaviour is materially cheaper to wrap than to reimplement in TypeScript.

## Revisiting this decision

A strategy change is a **minor bump** at minimum and likely a **major** because consumers see different install-time artefacts (prebuilt binaries, WASM glue, postinstall scripts). Do not switch strategies silently — open an ADR-style issue first and link it from this file.
