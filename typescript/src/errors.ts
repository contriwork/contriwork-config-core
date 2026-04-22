/**
 * Error taxonomy — mirrors CONTRACT.md §Error Taxonomy (v1).
 *
 * Every subclass exposes a stable `code` string that is identical across
 * the Python, C#, and TypeScript implementations. The class names are
 * local TypeScript convention and may be renamed without a contract bump;
 * the codes never are.
 */

/** Base class for every ConfigCore error. */
export class ConfigError extends Error {
  /** Stable, cross-language error code. Overridden per subclass. */
  readonly code: string = "";

  /** Optional structured diagnostic payload (e.g. validator issue list). */
  readonly details?: unknown;

  constructor(message: string, details?: unknown) {
    super(message);
    this.name = this.constructor.name;
    if (details !== undefined) {
      this.details = details;
    }
  }
}

/** The merged, resolved config did not validate against the schema. */
export class ValidationFailed extends ConfigError {
  override readonly code = "VALIDATION_FAILED";
}

/** A declared source could not be opened or read. */
export class SourceUnavailable extends ConfigError {
  override readonly code = "SOURCE_UNAVAILABLE";
}

/** A source's content could not be parsed into a dict. */
export class SourceParseFailed extends ConfigError {
  override readonly code = "SOURCE_PARSE_FAILED";
}

/** A `${...}` reference did not match the documented syntax. */
export class SecretRefMalformed extends ConfigError {
  override readonly code = "SECRET_REF_MALFORMED";
}

/** A reference's scheme has no registered resolver. */
export class SecretSchemeUnsupported extends ConfigError {
  override readonly code = "SECRET_SCHEME_UNSUPPORTED";
}

/** A resolver accepted the scheme but could not produce a value. */
export class SecretRefUnresolved extends ConfigError {
  override readonly code = "SECRET_REF_UNRESOLVED";
}
