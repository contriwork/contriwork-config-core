/**
 * Public entry point: `loadConfig`.
 *
 * Order of operations (CONTRACT.md §Behavior):
 * 1. `Source.snapshot()` for each source, in declared order.
 * 2. Deep-merge; later sources override earlier.
 * 3. Walk string leaves and resolve `${scheme:value}` via the resolver.
 * 4. Validate via the schema adapter.
 */

import { ValidationFailed } from "./errors.js";
import {
  EnvResolver,
  NullResolver,
  type SecretResolver,
} from "./resolvers.js";
import type { SchemaAdapter } from "./schema.js";
import type { Source } from "./sources.js";
import { deepMerge, isPlainObject } from "./_merge.js";
import { resolveRefs } from "./_refs.js";

export interface LoadConfigOptions<T> {
  schema: SchemaAdapter<T>;
  sources: readonly Source[];
  /**
   * - `undefined` (omitted): default {@link EnvResolver} is used.
   * - `null`: secret resolution is disabled; refs pass through verbatim.
   *   Internally mapped to a {@link NullResolver} so the resolution path
   *   stays uniform — the two are semantically equivalent.
   * - Any {@link SecretResolver} (including {@link NullResolver}
   *   directly): used as-is.
   */
  resolver?: SecretResolver | null;
}

/**
 * Load, merge, resolve, and validate configuration.
 *
 * @throws {@link ValidationFailed} when the sources list is empty or final
 *   validation fails.
 * @throws Secret-ref exceptions from the resolver.
 */
export async function loadConfig<T>(options: LoadConfigOptions<T>): Promise<T> {
  const { schema, sources } = options;

  if (sources.length === 0) {
    throw new ValidationFailed("loadConfig requires at least one source");
  }

  let merged: Record<string, unknown> = {};
  for (const source of sources) {
    const snapshot = await source.snapshot();
    merged = deepMerge(merged, snapshot);
  }

  const effectiveResolver: SecretResolver =
    options.resolver === undefined
      ? new EnvResolver()
      : (options.resolver ?? new NullResolver());

  const resolved = await resolveRefs(merged, effectiveResolver);
  const resolvedDict = isPlainObject(resolved) ? resolved : {};

  return schema.validate(resolvedDict);
}
