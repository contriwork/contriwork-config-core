import { describe, expect, it } from "vitest";

import { deepMerge } from "../src/_merge.js";

describe("deepMerge", () => {
  it("overlay replaces scalar", () => {
    expect(deepMerge({ a: 1 }, { a: 2 })).toEqual({ a: 2 });
  });

  it("overlay adds new key", () => {
    expect(deepMerge({ a: 1 }, { b: 2 })).toEqual({ a: 1, b: 2 });
  });

  it("nested objects recurse", () => {
    expect(
      deepMerge({ db: { url: "x", pool: 5 } }, { db: { pool: 10 } }),
    ).toEqual({ db: { url: "x", pool: 10 } });
  });

  it("array replaces not concats", () => {
    expect(deepMerge({ tags: ["a", "b"] }, { tags: ["c"] })).toEqual({
      tags: ["c"],
    });
  });

  it("object replaces scalar when types mismatch", () => {
    expect(deepMerge({ x: 1 }, { x: { nested: true } })).toEqual({
      x: { nested: true },
    });
  });

  it("scalar replaces object when types mismatch", () => {
    expect(deepMerge({ x: { nested: true } }, { x: "flat" })).toEqual({
      x: "flat",
    });
  });

  it("inputs are not mutated", () => {
    const base: Record<string, unknown> = { a: { b: 1 } };
    const overlay = { a: { c: 2 } };
    const merged = deepMerge(base, overlay) as { a: { b: number; c: number } };
    merged.a.b = 999;
    const baseA = base["a"] as { b: number };
    expect(baseA.b).toBe(1);
  });
});
