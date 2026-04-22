import { describe, expect, it } from "vitest";
import { z } from "zod";

import { ValidationFailed, ZodAdapter } from "../src/index.js";

const AppConfig = z.object({
  db_url: z.string(),
  debug: z.boolean().default(false),
  pool_size: z.number().int().min(1),
});

describe("ZodAdapter", () => {
  it("valid data returns typed instance", () => {
    const cfg = new ZodAdapter(AppConfig).validate({
      db_url: "sqlite://x",
      debug: true,
      pool_size: 5,
    });
    expect(cfg).toEqual({ db_url: "sqlite://x", debug: true, pool_size: 5 });
  });

  it("missing required throws ValidationFailed", () => {
    const fn = (): unknown =>
      new ZodAdapter(AppConfig).validate({ debug: true, pool_size: 5 });
    expect(fn).toThrowError(ValidationFailed);
    try {
      fn();
    } catch (e) {
      expect((e as ValidationFailed).code).toBe("VALIDATION_FAILED");
      expect((e as ValidationFailed).details).toBeDefined();
    }
  });

  it("constraint violation throws", () => {
    expect(() =>
      new ZodAdapter(AppConfig).validate({
        db_url: "x",
        debug: false,
        pool_size: 0, // < 1
      }),
    ).toThrowError(ValidationFailed);
  });

  it("applies default", () => {
    const cfg = new ZodAdapter(AppConfig).validate({
      db_url: "x",
      pool_size: 1,
    });
    expect(cfg.debug).toBe(false);
  });
});
