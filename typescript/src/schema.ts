/**
 * SchemaAdapter interface and the built-in zod adapter.
 *
 * ConfigCore does not bundle a schema library. An adapter is one method
 * wide — callers who prefer valibot, arktype, ajv, etc. can write their
 * own. The built-in {@link ZodAdapter} wraps a `zod` schema.
 */

import type { z, ZodTypeAny } from "zod";

import { ValidationFailed } from "./errors.js";

/** Validate a dict against a schema and return a typed instance. */
export interface SchemaAdapter<T> {
  validate(data: Record<string, unknown>): T;
}

/** Adapt a zod schema to the {@link SchemaAdapter} interface. */
export class ZodAdapter<S extends ZodTypeAny> implements SchemaAdapter<
  z.infer<S>
> {
  constructor(private readonly schema: S) {}

  validate(data: Record<string, unknown>): z.infer<S> {
    const result = this.schema.safeParse(data);
    if (!result.success) {
      throw new ValidationFailed(
        `config failed validation: ${result.error.issues.length} issue(s)`,
        result.error.issues,
      );
    }
    return result.data;
  }
}
