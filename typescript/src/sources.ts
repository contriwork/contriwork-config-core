/**
 * Source interface and built-in implementations.
 *
 * See CONTRACT.md §Port/`Source protocol`. A source exposes a snapshot of
 * configuration keys as a nested record. Built-ins: {@link EnvSource},
 * {@link FileSource} (yaml / json / toml), {@link InMemorySource}.
 */

import { readFile } from "node:fs/promises";
import { extname } from "node:path";
import { parse as parseToml } from "smol-toml";
import { parse as parseYaml } from "yaml";

import { SourceParseFailed, SourceUnavailable } from "./errors.js";

export type FileFormat = "yaml" | "json" | "toml";

/** A source of configuration keys. */
export interface Source {
  /**
   * Return a fresh snapshot of this source's current keys.
   *
   * @throws {@link SourceUnavailable} if the source cannot be opened.
   * @throws {@link SourceParseFailed} if the source content cannot be parsed.
   */
  snapshot(): Promise<Record<string, unknown>>;
}

/** A source backed by an object literal. Primarily for tests. */
export class InMemorySource implements Source {
  constructor(private readonly data: Record<string, unknown>) {}

  snapshot(): Promise<Record<string, unknown>> {
    return Promise.resolve({ ...this.data });
  }
}

export interface EnvSourceOptions {
  /** Only env keys starting with this are considered. Default: empty (all). */
  prefix?: string;
  /** Delimiter that introduces a nesting level. Default: `__`. */
  separator?: string;
}

/**
 * Read `process.env` and map flat keys to a nested record. Keys beginning
 * with the prefix are collected, the prefix is stripped, the remainder is
 * lowercased, and each `separator` occurrence becomes a nesting level.
 */
export class EnvSource implements Source {
  private readonly prefix: string;
  private readonly separator: string;

  constructor(options: EnvSourceOptions = {}) {
    const { prefix = "", separator = "__" } = options;
    if (separator.length === 0) {
      throw new Error("separator must be a non-empty string");
    }
    this.prefix = prefix;
    this.separator = separator;
  }

  snapshot(): Promise<Record<string, unknown>> {
    const result: Record<string, unknown> = {};
    for (const [rawKey, value] of Object.entries(process.env)) {
      if (value === undefined) continue;
      if (!rawKey.startsWith(this.prefix)) continue;
      const stripped = rawKey.slice(this.prefix.length);
      if (stripped.length === 0) continue;
      const path = stripped.toLowerCase().split(this.separator);
      setNested(result, path, value);
    }
    return Promise.resolve(result);
  }
}

export interface FileSourceOptions {
  /** Explicit format override. Inferred from extension if omitted. */
  format?: FileFormat;
  /**
   * If `true` (default), a missing file throws {@link SourceUnavailable}.
   * If `false`, a missing file yields an empty record.
   */
  required?: boolean;
}

/**
 * Load a YAML / JSON / TOML file from disk. Format is inferred from the
 * extension (`.yaml` / `.yml` / `.json` / `.toml`) unless passed
 * explicitly.
 */
export class FileSource implements Source {
  private readonly path: string;
  private readonly format: FileFormat;
  private readonly required: boolean;

  constructor(path: string, options: FileSourceOptions = {}) {
    const { format, required = true } = options;
    this.path = path;
    this.format = format ?? inferFormat(path);
    this.required = required;
  }

  async snapshot(): Promise<Record<string, unknown>> {
    let content: string;
    try {
      content = await readFile(this.path, "utf-8");
    } catch (e) {
      if (isFileNotFound(e)) {
        if (!this.required) {
          return {};
        }
        throw new SourceUnavailable(`config file not found: ${this.path}`);
      }
      const msg = e instanceof Error ? e.message : String(e);
      throw new SourceUnavailable(
        `cannot read config file ${this.path}: ${msg}`,
      );
    }

    try {
      const parsed = parse(content, this.format);
      if (parsed === null || parsed === undefined) {
        return {};
      }
      if (!isPlainRecord(parsed)) {
        throw new SourceParseFailed(
          `${this.path} (${this.format}) must contain a top-level mapping; got ${typeOf(parsed)}`,
        );
      }
      return parsed;
    } catch (e) {
      if (e instanceof SourceParseFailed) throw e;
      const msg = e instanceof Error ? e.message : String(e);
      throw new SourceParseFailed(
        `failed to parse ${this.path} as ${this.format}: ${msg}`,
      );
    }
  }
}

function inferFormat(path: string): FileFormat {
  const ext = extname(path).toLowerCase();
  if (ext === ".yaml" || ext === ".yml") return "yaml";
  if (ext === ".json") return "json";
  if (ext === ".toml") return "toml";
  throw new SourceParseFailed(
    `cannot infer format from ${path}; pass format explicitly (yaml / json / toml)`,
  );
}

function parse(content: string, format: FileFormat): unknown {
  if (content.trim().length === 0) return null;
  if (format === "yaml") return parseYaml(content) as unknown;
  if (format === "json") return JSON.parse(content) as unknown;
  if (format === "toml") return parseToml(content);
  throw new SourceParseFailed(`unsupported format: ${format as string}`);
}

function setNested(
  target: Record<string, unknown>,
  path: string[],
  value: string,
): void {
  let cursor = target;
  for (let i = 0; i < path.length - 1; i++) {
    const key = path[i];
    if (key === undefined) continue;
    const existing = cursor[key];
    if (isPlainRecord(existing)) {
      cursor = existing;
    } else {
      const next: Record<string, unknown> = {};
      cursor[key] = next;
      cursor = next;
    }
  }
  const last = path[path.length - 1];
  if (last !== undefined) {
    cursor[last] = value;
  }
}

function isPlainRecord(v: unknown): v is Record<string, unknown> {
  if (v === null || typeof v !== "object") return false;
  if (Array.isArray(v)) return false;
  const proto: unknown = Object.getPrototypeOf(v);
  return proto === null || proto === Object.prototype;
}

function typeOf(v: unknown): string {
  if (v === null) return "null";
  if (Array.isArray(v)) return "array";
  return typeof v;
}

function isFileNotFound(e: unknown): boolean {
  return (
    typeof e === "object" &&
    e !== null &&
    "code" in e &&
    (e as { code: unknown }).code === "ENOENT"
  );
}
