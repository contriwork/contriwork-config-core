/**
 * Internal secret-reference walker.
 *
 * Grammar:
 * - `${scheme:value}` — a reference. `scheme` matches `[a-z][a-z0-9_]*`.
 * - `$${literal}` — escape; resolves to a literal `${literal}`.
 *
 * Only string leaves of the parsed config tree are scanned.
 */

import { SecretRefMalformed } from "./errors.js";
import type { SecretResolver } from "./resolvers.js";
import { isPlainObject } from "./_merge.js";

const ESCAPE_SENTINEL = "\x00CCC_ESCAPED_BRACE_\x00";
const REF_PATTERN = /\$\{([a-z][a-z0-9_]*):([^}]*)\}/g;

export async function resolveRefs(
  data: unknown,
  resolver: SecretResolver | null,
): Promise<unknown> {
  if (resolver === null) {
    return data;
  }
  if (typeof data === "string") {
    return resolveString(data, resolver);
  }
  if (Array.isArray(data)) {
    const result: unknown[] = [];
    for (const item of data) {
      result.push(await resolveRefs(item, resolver));
    }
    return result;
  }
  if (isPlainObject(data)) {
    const result: Record<string, unknown> = {};
    for (const [k, v] of Object.entries(data)) {
      result[k] = await resolveRefs(v, resolver);
    }
    return result;
  }
  return data;
}

async function resolveString(
  text: string,
  resolver: SecretResolver,
): Promise<string> {
  const protectedText = text.replaceAll("$${", ESCAPE_SENTINEL);
  assertNoMalformed(protectedText, text);

  const parts: string[] = [];
  let lastEnd = 0;
  REF_PATTERN.lastIndex = 0;
  let m: RegExpExecArray | null;
  while ((m = REF_PATTERN.exec(protectedText)) !== null) {
    const scheme = m[1];
    const value = m[2];
    if (scheme === undefined || value === undefined) {
      throw new SecretRefMalformed(
        `malformed match at ${m.index} in '${text}'`,
      );
    }
    const resolved = await resolver.resolve(scheme, value);
    parts.push(protectedText.slice(lastEnd, m.index));
    parts.push(resolved);
    lastEnd = m.index + m[0].length;
  }
  parts.push(protectedText.slice(lastEnd));

  return parts.join("").replaceAll(ESCAPE_SENTINEL, "${");
}

function assertNoMalformed(protectedText: string, original: string): void {
  const localRe = /\$\{([a-z][a-z0-9_]*):([^}]*)\}/y;
  let idx = 0;
  while (true) {
    const start = protectedText.indexOf("${", idx);
    if (start === -1) {
      return;
    }
    localRe.lastIndex = start;
    const match = localRe.exec(protectedText);
    if (match === null) {
      throw new SecretRefMalformed(
        `malformed secret reference at offset ${start} in '${original}'`,
      );
    }
    idx = start + match[0].length;
  }
}
