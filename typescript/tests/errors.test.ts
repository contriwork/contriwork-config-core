import { describe, expect, it } from "vitest";

import {
  ConfigError,
  SecretRefMalformed,
  SecretRefUnresolved,
  SecretSchemeUnsupported,
  SourceParseFailed,
  SourceUnavailable,
  ValidationFailed,
} from "../src/index.js";

describe("error taxonomy", () => {
  it.each([
    [ValidationFailed, "VALIDATION_FAILED"],
    [SourceUnavailable, "SOURCE_UNAVAILABLE"],
    [SourceParseFailed, "SOURCE_PARSE_FAILED"],
    [SecretRefMalformed, "SECRET_REF_MALFORMED"],
    [SecretSchemeUnsupported, "SECRET_SCHEME_UNSUPPORTED"],
    [SecretRefUnresolved, "SECRET_REF_UNRESOLVED"],
  ] as const)("%s code stable", (Cls, expectedCode) => {
    const err = new Cls("boom");
    expect(err.code).toBe(expectedCode);
    expect(err).toBeInstanceOf(ConfigError);
    expect(err).toBeInstanceOf(Error);
  });

  it("details default to undefined", () => {
    const err = new ValidationFailed("boom");
    expect(err.details).toBeUndefined();
  });

  it("details can round-trip", () => {
    const payload = [{ path: "x", message: "required" }];
    const err = new ValidationFailed("boom", payload);
    expect(err.details).toBe(payload);
  });
});
