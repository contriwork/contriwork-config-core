import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";

import {
  EnvSource,
  FileSource,
  InMemorySource,
  SourceParseFailed,
  SourceUnavailable,
} from "../src/index.js";

function mkTmp(): string {
  return mkdtempSync(join(tmpdir(), "ccc-"));
}

describe("InMemorySource", () => {
  it("returns data", async () => {
    const src = new InMemorySource({ a: 1, b: { c: 2 } });
    expect(await src.snapshot()).toEqual({ a: 1, b: { c: 2 } });
  });

  it("caller cannot mutate source via snapshot", async () => {
    const data = { a: 1 };
    const src = new InMemorySource(data);
    const snap = await src.snapshot();
    (snap as Record<string, number>)["a"] = 999;
    expect(data.a).toBe(1);
  });
});

describe("EnvSource", () => {
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

  it("filters by prefix, strips it", async () => {
    setEnv("APP__DB__URL", "sqlite://app.db");
    setEnv("OTHER__IGNORED", "x");
    const snap = await new EnvSource({ prefix: "APP__" }).snapshot();
    expect(snap).toEqual({ db: { url: "sqlite://app.db" } });
  });

  it("custom separator", async () => {
    setEnv("APP_DB_URL", "pg://x");
    const snap = await new EnvSource({
      prefix: "APP_",
      separator: "_",
    }).snapshot();
    expect(snap).toEqual({ db: { url: "pg://x" } });
  });

  it("flat key without separator occurrence", async () => {
    setEnv("APP_DEBUG", "true");
    const snap = await new EnvSource({ prefix: "APP_" }).snapshot();
    expect(snap).toEqual({ debug: "true" });
  });

  it("prefix equal to key yields empty", async () => {
    setEnv("APP_", "bogus");
    const snap = await new EnvSource({ prefix: "APP_" }).snapshot();
    expect(snap).toEqual({});
  });

  it("empty separator throws", () => {
    expect(() => new EnvSource({ separator: "" })).toThrowError();
  });

  // ── decodeJsonFor ────────────────────────────────────────────────

  it("decode_json list", async () => {
    setEnv("APP_HOSTS", '["a", "b", "c"]');
    const snap = await new EnvSource({
      prefix: "APP_",
      decodeJsonFor: ["list"],
    }).snapshot();
    expect(snap).toEqual({ hosts: ["a", "b", "c"] });
  });

  it("decode_json dict", async () => {
    setEnv("APP_RATE_LIMITS", '{"market_data": 10}');
    const snap = await new EnvSource({
      prefix: "APP_",
      decodeJsonFor: ["dict"],
    }).snapshot();
    expect(snap).toEqual({ rate_limits: { market_data: 10 } });
  });

  it("decode_json off keeps raw string (back-compat)", async () => {
    setEnv("APP_HOSTS", '["a", "b"]');
    const snap = await new EnvSource({ prefix: "APP_" }).snapshot();
    expect(snap).toEqual({ hosts: '["a", "b"]' });
  });

  it("decode_json invalid falls back to raw string", async () => {
    setEnv("APP_HOSTS", "not-json");
    const snap = await new EnvSource({
      prefix: "APP_",
      decodeJsonFor: ["list"],
    }).snapshot();
    expect(snap).toEqual({ hosts: "not-json" });
  });

  it("decode_json wrong category falls back", async () => {
    setEnv("APP_DEBUG", "true");
    const snap = await new EnvSource({
      prefix: "APP_",
      decodeJsonFor: ["list"],
    }).snapshot();
    expect(snap).toEqual({ debug: "true" });
  });

  it("decode_json bool / int / float", async () => {
    setEnv("APP_DEBUG", "true");
    setEnv("APP_POOL", "10");
    setEnv("APP_RATIO", "0.5");
    const snap = await new EnvSource({
      prefix: "APP_",
      decodeJsonFor: ["bool", "int", "float"],
    }).snapshot();
    expect(snap).toEqual({ debug: true, pool: 10, ratio: 0.5 });
  });
});

describe("FileSource", () => {
  let dir: string;

  beforeEach(() => {
    dir = mkTmp();
  });
  afterEach(() => {
    rmSync(dir, { recursive: true, force: true });
  });

  it("yaml", async () => {
    const p = join(dir, "cfg.yaml");
    writeFileSync(p, "db:\n  url: sqlite://x\n  pool: 10\n");
    expect(await new FileSource(p).snapshot()).toEqual({
      db: { url: "sqlite://x", pool: 10 },
    });
  });

  it("json", async () => {
    const p = join(dir, "cfg.json");
    writeFileSync(p, '{"db": {"url": "sqlite://y"}}');
    expect(await new FileSource(p).snapshot()).toEqual({
      db: { url: "sqlite://y" },
    });
  });

  it("toml", async () => {
    const p = join(dir, "cfg.toml");
    writeFileSync(p, '[db]\nurl = "sqlite://z"\npool = 5\n');
    expect(await new FileSource(p).snapshot()).toEqual({
      db: { url: "sqlite://z", pool: 5 },
    });
  });

  it("format override", async () => {
    const p = join(dir, "weird.cfg");
    writeFileSync(p, '{"a": 1}');
    expect(await new FileSource(p, { format: "json" }).snapshot()).toEqual({
      a: 1,
    });
  });

  it("missing required throws SourceUnavailable", async () => {
    const p = join(dir, "nope.yaml");
    await expect(new FileSource(p).snapshot()).rejects.toBeInstanceOf(
      SourceUnavailable,
    );
  });

  it("missing optional returns empty", async () => {
    const p = join(dir, "nope.yaml");
    const snap = await new FileSource(p, { required: false }).snapshot();
    expect(snap).toEqual({});
  });

  it("unknown extension throws at construction", () => {
    expect(() => new FileSource("cfg.bin")).toThrowError(SourceParseFailed);
  });

  it("non-mapping root throws SourceParseFailed", async () => {
    const p = join(dir, "arr.yaml");
    writeFileSync(p, "- a\n- b\n");
    await expect(new FileSource(p).snapshot()).rejects.toBeInstanceOf(
      SourceParseFailed,
    );
  });

  it("empty file returns empty", async () => {
    const p = join(dir, "empty.json");
    writeFileSync(p, "");
    expect(await new FileSource(p).snapshot()).toEqual({});
  });

  it("missing directory required throws SourceUnavailable", async () => {
    const missingDir = join(dir, "nonexistent-subdir");
    mkdirSync(dir, { recursive: true });
    const p = join(missingDir, "cfg.yaml");
    await expect(new FileSource(p).snapshot()).rejects.toBeInstanceOf(
      SourceUnavailable,
    );
  });

  // ── dotenv format ────────────────────────────────────────────────

  it("dotenv basic", async () => {
    const p = join(dir, "app.env");
    writeFileSync(p, "DB_URL=sqlite://app.db\nDEBUG=true\n");
    expect(await new FileSource(p).snapshot()).toEqual({
      DB_URL: "sqlite://app.db",
      DEBUG: "true",
    });
  });

  it("dotenv quoted", async () => {
    const p = join(dir, "quoted.env");
    writeFileSync(p, "NAME=\"ContriWork Inc.\"\nMOTTO='with spaces'\n");
    expect(await new FileSource(p).snapshot()).toEqual({
      NAME: "ContriWork Inc.",
      MOTTO: "with spaces",
    });
  });

  it("dotenv comments and blanks", async () => {
    const p = join(dir, "mixed.env");
    writeFileSync(
      p,
      "# top\n\nKEY=value\n   # indented\nOTHER=v2\n",
    );
    expect(await new FileSource(p).snapshot()).toEqual({
      KEY: "value",
      OTHER: "v2",
    });
  });

  it("dotenv equals in value", async () => {
    const p = join(dir, "eq.env");
    writeFileSync(p, "URL=postgres://u:p=secret@host/db\n");
    expect(await new FileSource(p).snapshot()).toEqual({
      URL: "postgres://u:p=secret@host/db",
    });
  });

  it("dotenv empty value", async () => {
    const p = join(dir, "empty.env");
    writeFileSync(p, "EMPTY=\nOTHER=v\n");
    expect(await new FileSource(p).snapshot()).toEqual({
      EMPTY: "",
      OTHER: "v",
    });
  });

  it("dotenv export prefix stripped", async () => {
    const p = join(dir, "export.env");
    writeFileSync(p, "export FOO=bar\nBAZ=qux\n");
    expect(await new FileSource(p).snapshot()).toEqual({
      FOO: "bar",
      BAZ: "qux",
    });
  });

  it("dotenv explicit format on non-.env extension", async () => {
    const p = join(dir, "weird.cfg");
    writeFileSync(p, "KEY=value\n");
    expect(await new FileSource(p, { format: "dotenv" }).snapshot()).toEqual({
      KEY: "value",
    });
  });
});
