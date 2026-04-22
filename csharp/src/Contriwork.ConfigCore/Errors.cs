namespace Contriwork.ConfigCore;

/// <summary>
/// Base class for every ConfigCore error. Concrete subclasses carry a stable
/// <see cref="Code"/> string that matches CONTRACT.md §Error Taxonomy and is
/// identical across the Python, C#, and TypeScript implementations.
/// </summary>
public abstract class ConfigException : Exception
{
    /// <summary>The stable, cross-language error code for this failure.</summary>
    public abstract string Code { get; }

    /// <summary>Optional structured diagnostic payload (e.g., validator errors list).</summary>
    public object? Details { get; init; }

    /// <summary>Initializes a new instance with a human-readable message.</summary>
    protected ConfigException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance wrapping a lower-level exception.</summary>
    protected ConfigException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>The merged, resolved config did not validate against the schema.</summary>
public sealed class ValidationFailedException : ConfigException
{
    /// <inheritdoc />
    public override string Code => "VALIDATION_FAILED";

    /// <summary>Initializes a new instance.</summary>
    public ValidationFailedException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance wrapping the validator's exception.</summary>
    public ValidationFailedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>A declared source could not be opened or read.</summary>
public sealed class SourceUnavailableException : ConfigException
{
    /// <inheritdoc />
    public override string Code => "SOURCE_UNAVAILABLE";

    /// <summary>Initializes a new instance.</summary>
    public SourceUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance wrapping the underlying I/O error.</summary>
    public SourceUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>A source's content could not be parsed into a dict.</summary>
public sealed class SourceParseFailedException : ConfigException
{
    /// <inheritdoc />
    public override string Code => "SOURCE_PARSE_FAILED";

    /// <summary>Initializes a new instance.</summary>
    public SourceParseFailedException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance wrapping the parser exception.</summary>
    public SourceParseFailedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>A <c>${...}</c> reference did not match the documented syntax.</summary>
public sealed class SecretRefMalformedException : ConfigException
{
    /// <inheritdoc />
    public override string Code => "SECRET_REF_MALFORMED";

    /// <summary>Initializes a new instance.</summary>
    public SecretRefMalformedException(string message)
        : base(message)
    {
    }
}

/// <summary>A reference's scheme has no registered resolver.</summary>
public sealed class SecretSchemeUnsupportedException : ConfigException
{
    /// <inheritdoc />
    public override string Code => "SECRET_SCHEME_UNSUPPORTED";

    /// <summary>Initializes a new instance.</summary>
    public SecretSchemeUnsupportedException(string message)
        : base(message)
    {
    }
}

/// <summary>A resolver accepted the scheme but could not produce a value.</summary>
public sealed class SecretRefUnresolvedException : ConfigException
{
    /// <inheritdoc />
    public override string Code => "SECRET_REF_UNRESOLVED";

    /// <summary>Initializes a new instance.</summary>
    public SecretRefUnresolvedException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance wrapping the underlying I/O error.</summary>
    public SecretRefUnresolvedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
