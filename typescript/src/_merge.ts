/**
 * Internal deep-merge. Not part of the public surface.
 *
 * - Plain objects recurse key-wise.
 * - Arrays and scalars replace.
 * - Neither input is mutated.
 */

export function deepMerge(
  base: Record<string, unknown>,
  overlay: Record<string, unknown>,
): Record<string, unknown> {
  const result: Record<string, unknown> = { ...base };
  for (const [key, overlayVal] of Object.entries(overlay)) {
    const baseVal = result[key];
    if (isPlainObject(baseVal) && isPlainObject(overlayVal)) {
      result[key] = deepMerge(baseVal, overlayVal);
    } else {
      result[key] = overlayVal;
    }
  }
  return result;
}

export function isPlainObject(v: unknown): v is Record<string, unknown> {
  if (v === null || typeof v !== "object") {
    return false;
  }
  if (Array.isArray(v)) {
    return false;
  }
  const proto: unknown = Object.getPrototypeOf(v);
  return proto === null || proto === Object.prototype;
}
