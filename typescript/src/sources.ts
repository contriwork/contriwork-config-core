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

export type FileFormat = "yaml" | "json" | "toml" | "dotenv";

/**
 * Categories of JSON literal that {@link EnvSource} may decode when its
 * {@link EnvSourceOptions.decodeJsonFor} opt-in flag enables them. Mirrors
 * the Python `JsonCategory` Literal and the C# `JsonCategory` enum.
 */
export type JsonCategory = "list" | "dict" | "bool" | "int" | "float";

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
  /**
   * Opt-in JSON decode categories. Default: `undefined` (no decoding,
   * preserves the v0.1.0 behaviour). When set, each env value is
   * best-effort parsed with `JSON.parse`; the parsed value replaces the
   * raw string only when its category is in this list. Otherwise the raw
   * string is kept (the schema validator decides).
   */
  decodeJsonFor?: readonly JsonCategory[];
}

/**
 * Read `process.env` and map flat keys to a nested record. Keys beginning
 * with the prefix are collected, the prefix is stripped, the remainder is
 * lowercased, and each `separator` occurrence becomes a nesting level.
 */
export class EnvSource implements Source {
  private readonly prefix: string;
  private readonly separator: string;
  private readonly decodeJsonFor: ReadonlySet<JsonCategory>;

  constructor(options: EnvSourceOptions = {}) {
    const { prefix = "", separator = "__", decodeJsonFor } = options;
    if (separator.length === 0) {
      throw new Error("separator must be a non-empty string");
    }
    this.prefix = prefix;
    this.separator = separator;
    this.decodeJsonFor = new Set(decodeJsonFor ?? []);
  }

  snapshot(): Promise<Record<string, unknown>> {
    const result: Record<string, unknown> = {};
    for (const [rawKey, value] of Object.entries(process.env)) {
      if (value === undefined) continue;
      if (!rawKey.startsWith(this.prefix)) continue;
      const stripped = rawKey.slice(this.prefix.length);
      if (stripped.length === 0) continue;
      const path = stripped.toLowerCase().split(this.separator);
      const decoded = this.maybeDecode(value);
      setNested(result, path, decoded);
    }
    return Promise.resolve(result);
  }

  private maybeDecode(value: string): unknown {
    if (this.decodeJsonFor.size === 0) return value;
    let parsed: unknown;
    try {
      parsed = JSON.parse(value);
    } catch {
      return value;
    }
    const category = jsonCategoryOf(parsed);
    return category !== null && this.decodeJsonFor.has(category) ? parsed : value;
  }
}

function jsonCategoryOf(value: unknown): JsonCategory | null {
  if (Array.isArray(value)) return "list";
  if (value !== null && typeof value === "object") return "dict";
  if (typeof value === "boolean") return "bool";
  if (typeof value === "number") {
    return Number.isInteger(value) ? "int" : "float";
  }
  return null;
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
 * Load a YAML / JSON / TOML / dotenv file from disk. Format is inferred
 * from the extension (`.yaml` / `.yml` / `.json` / `.toml` / `.env`)
 * unless passed explicitly.
 *
 * The `"dotenv"` format reads `KEY=VALUE` lines from a `.env`-style file.
 * The result is a **flat** record with **verbatim** keys (no lowercasing,
 * no nesting) — callers wanting `EnvSource`-style transformation should
 * compose this with their own schema. Subset supported: full-line `#`
 * comments, blank lines, optional `export` prefix, surrounding single or
 * double quotes around the value. Out of scope: variable interpolation
 * (`KEY=$OTHER`), multi-line values, in-quote escapes.
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
  if (ext === ".env") return "dotenv";
  throw new SourceParseFailed(
    `cannot infer format from ${path}; pass format explicitly (yaml / json / toml / dotenv)`,
  );
}

function parse(content: string, format: FileFormat): unknown {
  // dotenv treats an empty file as an empty record — handle it before the
  // generic blank-string short-circuit used by yaml/json/toml.
  if (format === "dotenv") return parseDotenv(content);
  if (content.trim().length === 0) return null;
  if (format === "yaml") return parseYaml(content) as unknown;
  if (format === "json") return JSON.parse(content) as unknown;
  if (format === "toml") return parseToml(content);
  throw new SourceParseFailed(`unsupported format: ${format as string}`);
}

function parseDotenv(content: string): Record<string, string> {
  const result: Record<string, string> = {};
  for (const rawLine of content.split("\n")) {
    let line = rawLine.replace(/\r$/, "").trim();
    if (line.length === 0 || line.startsWith("#")) continue;
    if (line.startsWith("export ")) {
      line = line.slice("export ".length).trimStart();
    }
    const eqIdx = line.indexOf("=");
    if (eqIdx < 0) continue;
    const key = line.slice(0, eqIdx).trim();
    if (key.length === 0) continue;
    let value = line.slice(eqIdx + 1).trim();
    if (
      value.length >= 2 &&
      value[0] === value[value.length - 1] &&
      (value[0] === '"' || value[0] === "'")
    ) {
      value = value.slice(1, -1);
    }
    result[key] = value;
  }
  return result;
}

function setNested(
  target: Record<string, unknown>,
  path: string[],
  value: unknown,
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
