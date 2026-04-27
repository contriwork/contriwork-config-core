/**
 * Helpers for unwrapping nullable secret-string config fields safely.
 *
 * Three-language parity: the Python adapter exposes `secret_str_or_empty`
 * / `secret_str_required` (with a `pydantic.SecretStr` input type) and the
 * .NET adapter exposes `SecretStrOrEmpty` / `SecretStrRequired`.
 * TypeScript treats secrets as plain `string`; the helpers exist for API
 * discoverability and naming parity rather than to wrap a custom type.
 */

/**
 * Returns the value when set, or `""` when `null` or `undefined`.
 * Centralizes the null-coalescing pattern at every read site for optional
 * secret config fields.
 */
export function secretStrOrEmpty(value: string | null | undefined): string {
  return value ?? "";
}

/**
 * Returns the value when set, or throws an `Error` naming `fieldName` when
 * `null` or `undefined`. Use for required secret fields where a missing
 * value is a startup-time configuration bug.
 *
 * @throws when `value` is `null` or `undefined`.
 */
export function secretStrRequired(
  value: string | null | undefined,
  fieldName: string,
): string {
  if (typeof fieldName !== "string" || fieldName.length === 0) {
    throw new Error("secretStrRequired: fieldName must be a non-empty string");
  }
  if (value === null || value === undefined) {
    throw new Error(`required secret field '${fieldName}' is null`);
  }
  return value;
}
