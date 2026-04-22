import { describe, expect, it } from "vitest";

import { resolveRefs } from "../src/_refs.js";
import type { SecretResolver } from "../src/resolvers.js";
import { SecretRefMalformed } from "../src/errors.js";

class FakeResolver implements SecretResolver {
  constructor(private readonly table: Record<string, string>) {}
  resolve(scheme: string, value: string): Promise<string> {
    const key = `${scheme}:${value}`;
    const v = this.table[key];
    if (v === undefined) throw new Error(`no entry for ${key}`);
    return Promise.resolve(v);
  }
}

describe("resolveRefs", () => {
  it("null resolver passes data through", async () => {
    const data = { x: "${env:FOO}" };
    expect(await resolveRefs(data, null)).toEqual(data);
  });

  it("single ref is resolved", async () => {
    const r = new FakeResolver({ "env:FOO": "bar" });
    expect(await resolveRefs("${env:FOO}", r)).toBe("bar");
  });

  it("multiple refs in one string", async () => {
    const r = new FakeResolver({
      "env:USER": "alice",
      "env:HOST": "h.example",
    });
    expect(await resolveRefs("${env:USER}@${env:HOST}", r)).toBe(
      "alice@h.example",
    );
  });

  it("refs with surrounding text", async () => {
    const r = new FakeResolver({ "env:PORT": "5432" });
    expect(await resolveRefs("tcp://db:${env:PORT}/app", r)).toBe(
      "tcp://db:5432/app",
    );
  });

  it("walks nested objects", async () => {
    const r = new FakeResolver({ "env:URL": "sqlite:///app.db" });
    const data = { db: { url: "${env:URL}" }, debug: false };
    expect(await resolveRefs(data, r)).toEqual({
      db: { url: "sqlite:///app.db" },
      debug: false,
    });
  });

  it("walks arrays", async () => {
    const r = new FakeResolver({ "env:A": "1", "env:B": "2" });
    const data = { items: ["${env:A}", "plain", "${env:B}"] };
    expect(await resolveRefs(data, r)).toEqual({ items: ["1", "plain", "2"] });
  });

  it("double-brace escape yields literal", async () => {
    const r = new FakeResolver({});
    expect(await resolveRefs("$${env:FOO}", r)).toBe("${env:FOO}");
  });

  it("escape is not resolved", async () => {
    const r = new FakeResolver({ "env:X": "should-not-appear" });
    expect(await resolveRefs("$${env:X}", r)).toBe("${env:X}");
  });

  it("malformed: unclosed brace", async () => {
    const r = new FakeResolver({});
    await expect(resolveRefs("${env:FOO", r)).rejects.toBeInstanceOf(
      SecretRefMalformed,
    );
  });

  it("malformed: no colon", async () => {
    const r = new FakeResolver({});
    await expect(resolveRefs("${env_FOO}", r)).rejects.toBeInstanceOf(
      SecretRefMalformed,
    );
  });

  it("malformed: uppercase scheme", async () => {
    const r = new FakeResolver({});
    await expect(resolveRefs("${ENV:FOO}", r)).rejects.toBeInstanceOf(
      SecretRefMalformed,
    );
  });

  it("numbers pass through unchanged", async () => {
    const r = new FakeResolver({});
    expect(await resolveRefs({ n: 42 }, r)).toEqual({ n: 42 });
  });

  it("booleans pass through unchanged", async () => {
    const r = new FakeResolver({});
    expect(await resolveRefs({ b: true }, r)).toEqual({ b: true });
  });
});
