using System.Collections.Generic;

namespace Contriwork.ConfigCore;

/// <summary>
/// Validate a dictionary against a schema and return a typed instance.
/// Cross-language: Python <c>SchemaAdapter</c> protocol, TypeScript
/// <c>SchemaAdapter</c> interface.
/// </summary>
public interface ISchemaAdapter<out T>
{
    /// <summary>
    /// Validate <paramref name="data"/>; return the typed result or throw
    /// <see cref="ValidationFailedException"/>.
    /// </summary>
    T Validate(IReadOnlyDictionary<string, object?> data);
}
