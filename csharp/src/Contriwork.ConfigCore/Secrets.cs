using System;

namespace Contriwork.ConfigCore;

/// <summary>
/// Helpers for unwrapping nullable secret-string config fields safely.
///
/// Three-language parity: the Python adapter exposes
/// <c>secret_str_or_empty</c> / <c>secret_str_required</c> (with a
/// <c>pydantic.SecretStr</c> input type) and the TypeScript adapter exposes
/// <c>secretStrOrEmpty</c> / <c>secretStrRequired</c>. .NET treats secrets
/// as plain <see cref="string"/>; the helpers exist for API
/// discoverability and naming parity rather than to wrap a custom type.
/// </summary>
public static class Secrets
{
    /// <summary>
    /// Returns <paramref name="value"/> when non-<c>null</c>, or
    /// <see cref="string.Empty"/> otherwise. Centralizes the
    /// null-coalescing pattern at every read site for optional secret
    /// config fields.
    /// </summary>
    public static string SecretStrOrEmpty(string? value) => value ?? string.Empty;

    /// <summary>
    /// Returns <paramref name="value"/> when non-<c>null</c>, or throws
    /// <see cref="ArgumentException"/> naming <paramref name="fieldName"/>
    /// otherwise. Use for required secret fields where a missing value is
    /// a startup-time configuration bug.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is <c>null</c>.
    /// </exception>
    public static string SecretStrRequired(string? value, string fieldName)
    {
        ArgumentException.ThrowIfNullOrEmpty(fieldName);
        if (value is null)
        {
            throw new ArgumentException(
                $"required secret field '{fieldName}' is null",
                nameof(value));
        }
        return value;
    }
}
