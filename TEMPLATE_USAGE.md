# Template Usage

You just clicked **Use this template** on `contriwork-repo-template`. Follow these steps in order. Do NOT push a release tag until step 10 is green.

The placeholder tokens used throughout the template:

| Context | Placeholder | Replace with (example `config-core`) |
|---------|-------------|--------------------------------------|
| Python dist name | `contriwork-config-core` | `contriwork-config-core` |
| Python import | `contriwork_config_core` | `contriwork_config_core` |
| C# namespace / assembly | `Contriwork.ConfigCore` | `Contriwork.ConfigCore` |
| TypeScript symbol | `ConfigCore` | `ConfigCore` |
| npm package | `@contriwork/config-core` | `@contriwork/config-core` |

---

## 1. Rename directories

```bash
git mv python/src/contriwork_config_core python/src/contriwork_<your_name>
git mv csharp/src/Contriwork.ConfigCore csharp/src/Contriwork.<YourName>
git mv csharp/tests/Contriwork.ConfigCore.Tests csharp/tests/Contriwork.<YourName>.Tests
```

## 2. Global find-and-replace

Use your editor's project-wide replace (case-sensitive). Do the replacements in this order to avoid partial-match collisions:

1. `@contriwork/config-core` → `@contriwork/<your-name>` (kebab-case, npm)
2. `contriwork-config-core` → `contriwork-<your-name>` (kebab-case, PyPI)
3. `contriwork_config_core` → `contriwork_<your_name>` (snake_case, Python import)
4. `Contriwork.ConfigCore` → `Contriwork.<YourName>` (PascalCase, C#)
5. `ConfigCore` → `<YourName>` (PascalCase, TypeScript symbols)
6. `PACKAGE_NAME` → `<your-name>` or `<your_name>` — context-dependent; verify by diff.

Rename C# solution file:

```bash
git mv csharp/Contriwork.ConfigCore.sln csharp/Contriwork.<YourName>.sln
```

## 3. Pin Dockerfile base image digests

Every `FROM` line carries a `@sha256:TODO` placeholder. Pin each to a current digest:

```bash
docker pull python:3.13-slim-trixie
docker inspect --format '{{index .RepoDigests 0}}' python:3.13-slim-trixie
# copy the @sha256:... suffix into python/Dockerfile
```

Repeat for:

- `python/Dockerfile` — build stage `python:3.13-slim-trixie`, runtime stage `python:3.13-slim-trixie`.
- `csharp/Dockerfile` — build stage `mcr.microsoft.com/dotnet/sdk:10.0`, runtime stage `mcr.microsoft.com/dotnet/runtime:10.0-jammy-chiseled-extra`.
- `typescript/Dockerfile` — build stage `node:24-bookworm-slim`, runtime stage `node:24-alpine`.

## 4. Fill `CONTRACT.md`

The contract is the single source of truth. Complete every `TODO` block before writing implementation code. A PR that touches public behavior without updating `CONTRACT.md` is rejected by the PR checklist.

## 5. Fill `README.md`

Replace the `TODO` blocks in the `## Why` and `## Quick start` sections. Badges already point at the correct per-registry URLs once step 2 is done.

## 6. Register PyPI Trusted Publisher

1. Go to <https://pypi.org/manage/account/publishing/> (sign in with a PyPI account that owns the package name — reserve the name first if needed).
2. Add a **pending publisher** with:
   - PyPI Project Name: `contriwork-<your-name>`
   - Owner: `contriwork`
   - Repository name: `contriwork-<your-name>`
   - Workflow: `release-python.yml`
   - Environment: `pypi`
3. First successful publish converts the pending publisher into a permanent one.

## 7. Register npm Trusted Publisher

1. Sign in to <https://www.npmjs.com/> as a member of the `contriwork` org.
2. Under **Packages → Publishing access**, add a Trusted Publisher for `@contriwork/<your-name>`:
   - Repository: `contriwork/contriwork-<your-name>`
   - Workflow: `release-npm.yml`
   - Environment: `npm`
3. Package must exist (publish a `0.0.0` placeholder first if needed) or be scoped-reserved by the org.

> **NuGet Trusted Publisher** is automatically validated from the OIDC token emitted by the `release-dotnet.yml` workflow. Configure the package on <https://www.nuget.org/> with the `contriwork` org's registered GitHub identity.

## 8. Enable branch ruleset

In **Settings → Rules → Rulesets** for this repo, apply the org default ruleset for `main`:

- Require signed commits.
- Require linear history.
- Required status checks: `ci / python`, `ci / csharp`, `ci / typescript`, `ci / contract`, `security-scan / *`.
- Block force push, block deletion.
- **Include administrators — ON.** No bypass.

If the org ruleset is not yet configured, apply the same rules as a repo-level branch protection rule temporarily.

## 9. Verify locally

```bash
pre-commit install --install-hooks
pre-commit run --all-files
cd python && uv sync && uv run pytest && uv run ruff check && uv run mypy src && cd ..
cd csharp && dotnet restore && dotnet build && dotnet test && dotnet format --verify-no-changes && cd ..
cd typescript && pnpm install --frozen-lockfile && pnpm build && pnpm test && pnpm lint && pnpm typecheck && cd ..
hadolint python/Dockerfile csharp/Dockerfile typescript/Dockerfile
```

Every step green → proceed. Any red → fix before tagging.

## 10. Initial release

All three publish workflows gate on CI being green on the tagged commit. If any of PyPI / NuGet / npm publish fails, the GitHub Release is marked failed and consumers must not adopt that tag.

```bash
# bump VERSION to 0.1.0 and add a row to VERSION_MATRIX.md
echo "0.1.0" > VERSION
# edit CHANGELOG.md: move [Unreleased] to [0.1.0] with all three language sub-sections
git add VERSION VERSION_MATRIX.md CHANGELOG.md
git commit -s -S -m "chore(release): 0.1.0"
git tag -s v0.1.0 -m "v0.1.0"
git push origin main --tags
```

### If a publish step fails

- The tag stays in git, but the release is invalid. Do NOT retry the same tag — registries may reject a second attempt at the same version.
- Diagnose the root cause (check the Actions run logs; common issues: Trusted Publisher not registered, OIDC claim mismatch, package name collision, SBOM artifact upload timeout).
- Delete the remote tag, bump the patch (`0.1.1`), add a CHANGELOG note explaining the skipped version, and re-release:

  ```bash
  git push --delete origin v0.1.0
  git tag -d v0.1.0
  echo "0.1.1" > VERSION
  # update CHANGELOG.md and VERSION_MATRIX.md to mark 0.1.0 as "failed — never published"
  git commit -s -S -am "chore(release): skip 0.1.0, re-release as 0.1.1"
  git tag -s v0.1.1 -m "v0.1.1"
  git push origin main --tags
  ```

- Rolling back is **per-tag, not per-registry**. If one of the three succeeded and two failed, the one that succeeded is still on its registry — document it in `CHANGELOG.md` under the failed version and supersede it with the next tag.
