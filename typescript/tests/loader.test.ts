import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { z } from "zod";

import {
  EnvResolver,
  EnvSource,
  FileSource,
  InMemorySource,
  SecretRefUnresolved,
  ValidationFailed,
  ZodAdapter,
  loadConfig,
} from "../src/index.js";

const AppConfig = z.object({
  db_url: z.string(),
  debug: z.boolean().default(false),
});

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

describe("loadConfig", () => {
  it("single source round-trip", async () => {
    const cfg = await loadConfig({
      schema: new ZodAdapter(AppConfig),
      sources: [new InMemorySource({ db_url: "sqlite://x", debug: true })],
    });
    expect(cfg.db_url).toBe("sqlite://x");
    expect(cfg.debug).toBe(true);
  });

  it("later source overrides earlier", async () => {
    const cfg = await loadConfig({
      schema: new ZodAdapter(AppConfig),
      sources: [
        new InMemorySource({ db_url: "sqlite://default", debug: false }),
        new InMemorySource({ debug: true }),
      ],
    });
    expect(cfg.db_url).toBe("sqlite://default");
    expect(cfg.debug).toBe(true);
  });

  it("empty sources throws ValidationFailed", async () => {
    await expect(
      loadConfig({ schema: new ZodAdapter(AppConfig), sources: [] }),
    ).rejects.toBeInstanceOf(ValidationFailed);
  });

  it("default resolver is EnvResolver", async () => {
    setEnv("LOADER_DB_URL", "pg://from-env");
    const cfg = await loadConfig({
      schema: new ZodAdapter(AppConfig),
      sources: [new InMemorySource({ db_url: "${env:LOADER_DB_URL}" })],
    });
    expect(cfg.db_url).toBe("pg://from-env");
  });

  it("explicit null resolver disables refs", async () => {
    const cfg = await loadConfig({
      schema: new ZodAdapter(AppConfig),
      sources: [new InMemorySource({ db_url: "${env:NOT_INTERPOLATED}" })],
      resolver: null,
    });
    expect(cfg.db_url).toBe("${env:NOT_INTERPOLATED}");
  });

  it("explicit resolver is used", async () => {
    setEnv("LOADER_X", "y");
    const cfg = await loadConfig({
      schema: new ZodAdapter(AppConfig),
      sources: [new InMemorySource({ db_url: "${env:LOADER_X}" })],
      resolver: new EnvResolver(),
    });
    expect(cfg.db_url).toBe("y");
  });

  it("unresolved secret propagates", async () => {
    await expect(
      loadConfig({
        schema: new ZodAdapter(AppConfig),
        sources: [new InMemorySource({ db_url: "${env:CCC_MISSING_LOADER}" })],
      }),
    ).rejects.toBeInstanceOf(SecretRefUnresolved);
  });

  it("missing required field fails validation", async () => {
    await expect(
      loadConfig({
        schema: new ZodAdapter(AppConfig),
        sources: [new InMemorySource({ debug: true })],
      }),
    ).rejects.toBeInstanceOf(ValidationFailed);
  });

  describe("file + env merge", () => {
    let dir: string;
    beforeEach(() => {
      dir = mkdtempSync(join(tmpdir(), "ccc-load-"));
    });
    afterEach(() => {
      rmSync(dir, { recursive: true, force: true });
    });

    it("file provides default, env overrides (schema coerces)", async () => {
      // Env vars are always strings; user's schema is responsible for coercion.
      // This test uses z.coerce to reflect the idiomatic zod+env pattern.
      const CoercingConfig = z.object({
        db_url: z.string(),
        debug: z.coerce.boolean().default(false),
      });
      const p = join(dir, "cfg.yaml");
      writeFileSync(p, "db_url: sqlite://file-default\ndebug: false\n");
      setEnv("LOADER_PREFIX_DEBUG", "true");
      const cfg = await loadConfig({
        schema: new ZodAdapter(CoercingConfig),
        sources: [
          new FileSource(p),
          new EnvSource({ prefix: "LOADER_PREFIX_" }),
        ],
      });
      expect(cfg.db_url).toBe("sqlite://file-default");
      expect(cfg.debug).toBe(true);
    });
  });
});
