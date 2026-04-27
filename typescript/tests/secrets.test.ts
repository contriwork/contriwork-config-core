import { describe, expect, it } from "vitest";

import { secretStrOrEmpty, secretStrRequired } from "../src/index.js";

describe("secretStrOrEmpty", () => {
  it("returns the value when set", () => {
    expect(secretStrOrEmpty("hunter2")).toBe("hunter2");
  });

  it("returns '' when null", () => {
    expect(secretStrOrEmpty(null)).toBe("");
  });

  it("returns '' when undefined", () => {
    expect(secretStrOrEmpty(undefined)).toBe("");
  });

  it("returns '' when value is the empty string (not coerced to null)", () => {
    expect(secretStrOrEmpty("")).toBe("");
  });
});

describe("secretStrRequired", () => {
  it("returns the value when set", () => {
    expect(secretStrRequired("hunter2", "db_password")).toBe("hunter2");
  });

  it("throws naming the field when null", () => {
    expect(() => secretStrRequired(null, "db_password")).toThrowError(
      /db_password/,
    );
  });

  it("throws naming the field when undefined", () => {
    expect(() => secretStrRequired(undefined, "openai_api_key")).toThrowError(
      /openai_api_key/,
    );
  });

  it("throws when fieldName is empty", () => {
    expect(() => secretStrRequired("hunter2", "")).toThrowError(/fieldName/);
  });
});
