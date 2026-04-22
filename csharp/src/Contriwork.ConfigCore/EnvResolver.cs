using System;
using System.Threading;
using System.Threading.Tasks;

namespace Contriwork.ConfigCore;

/// <summary>Resolve <c>${env:NAME}</c> from the process environment.</summary>
public sealed class EnvResolver : ISecretResolver
{
    private const string Scheme = "env";

    /// <inheritdoc />
    public Task<string> ResolveAsync(string scheme, string value, CancellationToken cancellationToken = default)
    {
        if (scheme != Scheme)
        {
            throw new SecretSchemeUnsupportedException(
                $"EnvResolver does not handle scheme '{scheme}'");
        }
        if (string.IsNullOrEmpty(value))
        {
            throw new SecretRefUnresolvedException("${env:} requires a variable name");
        }
        var resolved = Environment.GetEnvironmentVariable(value);
        if (resolved is null)
        {
            throw new SecretRefUnresolvedException($"env var not set: {value}");
        }
        return Task.FromResult(resolved);
    }
}
