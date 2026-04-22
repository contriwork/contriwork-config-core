import { describe, expect, it } from "vitest";

import {
  ChainResolver,
  ConfigError,
  EnvResolver,
  EnvSource,
  FileResolver,
  FileSource,
  InMemorySource,
  ZodAdapter,
  loadConfig,
} from "../src/index.js";

describe("smoke", () => {
  it("public surface is importable", () => {
    expect(loadConfig).toBeInstanceOf(Function);
    expect(EnvSource).toBeInstanceOf(Function);
    expect(FileSource).toBeInstanceOf(Function);
    expect(InMemorySource).toBeInstanceOf(Function);
    expect(EnvResolver).toBeInstanceOf(Function);
    expect(FileResolver).toBeInstanceOf(Function);
    expect(ChainResolver).toBeInstanceOf(Function);
    expect(ZodAdapter).toBeInstanceOf(Function);
    expect(ConfigError).toBeInstanceOf(Function);
  });
});
