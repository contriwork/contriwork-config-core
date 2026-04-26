/**
 * @contriwork/config-core — TypeScript adapter.
 *
 * Cross-language static configuration loader. See CONTRACT.md for the
 * language-agnostic specification; this module is the Node.js / TypeScript
 * binding.
 *
 * @example
 * ```ts
 * import { z } from "zod";
 * import { loadConfig, EnvSource, FileSource, ZodAdapter } from "@contriwork/config-core";
 *
 * const AppConfig = z.object({
 *   db_url: z.string(),
 *   debug: z.boolean().default(false),
 * });
 *
 * const cfg = await loadConfig({
 *   schema: new ZodAdapter(AppConfig),
 *   sources: [new FileSource("./cfg.yml"), new EnvSource({ prefix: "APP_" })],
 * });
 * ```
 */

export {
  ConfigError,
  SecretRefMalformed,
  SecretRefUnresolved,
  SecretSchemeUnsupported,
  SourceParseFailed,
  SourceUnavailable,
  ValidationFailed,
} from "./errors.js";
export { loadConfig, type LoadConfigOptions } from "./loader.js";
export {
  ChainResolver,
  EnvResolver,
  FileResolver,
  type FileResolverOptions,
  type SecretResolver,
} from "./resolvers.js";
export { type SchemaAdapter, ZodAdapter } from "./schema.js";
export { secretStrOrEmpty, secretStrRequired } from "./secrets.js";
export {
  EnvSource,
  type EnvSourceOptions,
  type FileFormat,
  FileSource,
  type FileSourceOptions,
  InMemorySource,
  type JsonCategory,
  type Source,
} from "./sources.js";
