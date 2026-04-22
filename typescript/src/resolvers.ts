/**
 * SecretResolver interface and built-in implementations.
 *
 * See CONTRACT.md §Port/`SecretResolver protocol`. Each resolver handles one
 * or more schemes of `${scheme:value}`. Unknown schemes throw
 * {@link SecretSchemeUnsupported}; recognised-but-unresolvable values throw
 * {@link SecretRefUnresolved}. {@link ChainResolver} composes several.
 */

import { readFile } from "node:fs/promises";
import { isAbsolute, join } from "node:path";

import { SecretRefUnresolved, SecretSchemeUnsupported } from "./errors.js";

/** Resolve a `${scheme:value}` reference to a plain string. */
export interface SecretResolver {
  resolve(scheme: string, value: string): Promise<string>;
}

/** Resolve `${env:NAME}` from `process.env`. */
export class EnvResolver implements SecretResolver {
  static readonly scheme = "env";

  async resolve(scheme: string, value: string): Promise<string> {
    if (scheme !== EnvResolver.scheme) {
      throw new SecretSchemeUnsupported(
        `EnvResolver does not handle scheme '${scheme}'`,
      );
    }
    if (value.length === 0) {
      throw new SecretRefUnresolved("${env:} requires a variable name");
    }
    const resolved = process.env[value];
    if (resolved === undefined) {
      throw new SecretRefUnresolved(`env var not set: ${value}`);
    }
    return Promise.resolve(resolved);
  }
}

export interface FileResolverOptions {
  /** Directory used to resolve relative paths. Absolute paths ignore this. */
  baseDir?: string;
}

/**
 * Resolve `${file:path}` by reading the file's contents. Trailing whitespace
 * (including newlines) is stripped — matches the common "secret in a
 * single-line file" pattern used by Docker / Kubernetes.
 */
export class FileResolver implements SecretResolver {
  static readonly scheme = "file";
  private readonly baseDir: string | undefined;

  constructor(options: FileResolverOptions = {}) {
    this.baseDir = options.baseDir;
  }

  async resolve(scheme: string, value: string): Promise<string> {
    if (scheme !== FileResolver.scheme) {
      throw new SecretSchemeUnsupported(
        `FileResolver does not handle scheme '${scheme}'`,
      );
    }
    if (value.length === 0) {
      throw new SecretRefUnresolved("${file:} requires a path");
    }

    const path =
      !isAbsolute(value) && this.baseDir !== undefined
        ? join(this.baseDir, value)
        : value;

    try {
      const text = await readFile(path, "utf-8");
      return text.replace(/\s+$/, "");
    } catch (e) {
      const code = (e as { code?: string }).code;
      if (code === "ENOENT" || code === "ENOTDIR") {
        throw new SecretRefUnresolved(`secret file not found: ${path}`);
      }
      const msg = e instanceof Error ? e.message : String(e);
      throw new SecretRefUnresolved(`cannot read secret file ${path}: ${msg}`);
    }
  }
}

/**
 * Try each resolver in order; first one that handles the scheme wins. If
 * every resolver throws {@link SecretSchemeUnsupported}, the chain re-throws
 * {@link SecretSchemeUnsupported} naming the scheme. Any other exception
 * (notably {@link SecretRefUnresolved}) propagates immediately — scheme-
 * match wins the first handler.
 */
export class ChainResolver implements SecretResolver {
  private readonly resolvers: readonly SecretResolver[];

  constructor(...resolvers: SecretResolver[]) {
    this.resolvers = resolvers;
  }

  async resolve(scheme: string, value: string): Promise<string> {
    for (const r of this.resolvers) {
      try {
        return await r.resolve(scheme, value);
      } catch (e) {
        if (e instanceof SecretSchemeUnsupported) {
          continue;
        }
        throw e;
      }
    }
    throw new SecretSchemeUnsupported(
      `no resolver registered for scheme '${scheme}'`,
    );
  }
}
