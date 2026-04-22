import { describe, expect, it } from "vitest";
import type { ConfigCorePort } from "../src/index.js";

describe("smoke", () => {
  it("exports ConfigCorePort as a type", () => {
    const shape: ConfigCorePort = {
      example: async (input: string) => input,
    };
    expect(typeof shape.example).toBe("function");
  });
});
