import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";

import {
  ChainResolver,
  EnvResolver,
  FileResolver,
  SecretRefUnresolved,
  SecretSchemeUnsupported,
} from "../src/index.js";

function mkTmp(): string {
  return mkdtempSync(join(tmpdir(), "ccc-"));
}

const savedEnv: Record<string, string | undefined> = {};
const trackedKeys: string[] = [];

function setEnv(key: string, value: string): void {
  if (!(key in savedEnv)) savedEnv[key] = process.env[key];
  process.env[key] = value;
  trackedKeys.push(key);
}

afterEach(() => {
  for (const k of trackedKeys) {
    const prev = savedEnv[k];
    if (prev === undefined) delete process.env[k];
    else process.env[k] = prev;
  }
  trackedKeys.length = 0;
});

describe("EnvResolver", () => {
  it("resolves a set variable", async () => {
    setEnv("SECRET_TOKEN", "s3cr3t");
    expect(await new EnvResolver().resolve("env", "SECRET_TOKEN")).toBe(
      "s3cr3t",
    );
  });

  it("missing variable throws SecretRefUnresolved", async () => {
    await expect(
      new EnvResolver().resolve("env", "CCC_DEFINITELY_NOT_SET_XYZ"),
    ).rejects.toBeInstanceOf(SecretRefUnresolved);
  });

  it("empty name throws SecretRefUnresolved", async () => {
    await expect(new EnvResolver().resolve("env", "")).rejects.toBeInstanceOf(
      SecretRefUnresolved,
    );
  });

  it("wrong scheme throws SecretSchemeUnsupported", async () => {
    await expect(
      new EnvResolver().resolve("file", "whatever"),
    ).rejects.toBeInstanceOf(SecretSchemeUnsupported);
  });
});

describe("FileResolver", () => {
  let dir: string;

  beforeEach(() => {
    dir = mkTmp();
  });
  afterEach(() => {
    rmSync(dir, { recursive: true, force: true });
  });

  it("resolves absolute path", async () => {
    const p = join(dir, "s.txt");
    writeFileSync(p, "mysecret\n");
    expect(await new FileResolver().resolve("file", p)).toBe("mysecret");
  });

  it("strips trailing whitespace only", async () => {
    const p = join(dir, "s.txt");
    writeFileSync(p, "  value with spaces  \n\n");
    expect(await new FileResolver().resolve("file", p)).toBe(
      "  value with spaces",
    );
  });

  it("missing file throws SecretRefUnresolved", async () => {
    await expect(
      new FileResolver().resolve("file", join(dir, "nope")),
    ).rejects.toBeInstanceOf(SecretRefUnresolved);
  });

  it("empty value throws SecretRefUnresolved", async () => {
    await expect(new FileResolver().resolve("file", "")).rejects.toBeInstanceOf(
      SecretRefUnresolved,
    );
  });

  it("wrong scheme throws SecretSchemeUnsupported", async () => {
    await expect(
      new FileResolver().resolve("env", "whatever"),
    ).rejects.toBeInstanceOf(SecretSchemeUnsupported);
  });

  it("relative path with baseDir", async () => {
    writeFileSync(join(dir, "s.txt"), "relative-secret\n");
    const r = new FileResolver({ baseDir: dir });
    expect(await r.resolve("file", "s.txt")).toBe("relative-secret");
  });
});

describe("ChainResolver", () => {
  let dir: string;

  beforeEach(() => {
    dir = mkTmp();
  });
  afterEach(() => {
    rmSync(dir, { recursive: true, force: true });
  });

  it("first hit wins", async () => {
    setEnv("CHAIN_A", "env-value");
    const c = new ChainResolver(new EnvResolver(), new FileResolver());
    expect(await c.resolve("env", "CHAIN_A")).toBe("env-value");
  });

  it("skips unsupported to next", async () => {
    writeFileSync(join(dir, "s.txt"), "chained\n");
    const c = new ChainResolver(new EnvResolver(), new FileResolver());
    expect(await c.resolve("file", join(dir, "s.txt"))).toBe("chained");
  });

  it("no handler throws SecretSchemeUnsupported", async () => {
    const c = new ChainResolver(new EnvResolver(), new FileResolver());
    await expect(c.resolve("vault", "some/path")).rejects.toBeInstanceOf(
      SecretSchemeUnsupported,
    );
  });

  it("propagates unresolved (first handler wins)", async () => {
    const c = new ChainResolver(new EnvResolver(), new FileResolver());
    await expect(
      c.resolve("env", "CCC_DEFINITELY_NOT_SET_XYZ"),
    ).rejects.toBeInstanceOf(SecretRefUnresolved);
  });

  it("empty chain throws SecretSchemeUnsupported", async () => {
    await expect(
      new ChainResolver().resolve("env", "X"),
    ).rejects.toBeInstanceOf(SecretSchemeUnsupported);
  });
});
