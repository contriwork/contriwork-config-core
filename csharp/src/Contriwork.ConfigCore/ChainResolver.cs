using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Contriwork.ConfigCore;

/// <summary>
/// Try each resolver in order; first one that handles the scheme wins. If
/// every resolver raises <see cref="SecretSchemeUnsupportedException"/>,
/// <see cref="ChainResolver"/> raises it too naming the scheme. Any other
/// exception (notably <see cref="SecretRefUnresolvedException"/>) propagates
/// immediately — scheme-match wins the first handler.
/// </summary>
public sealed class ChainResolver : ISecretResolver
{
    private readonly IReadOnlyList<ISecretResolver> _resolvers;

    /// <summary>Initializes a new chain from the given resolvers.</summary>
    public ChainResolver(params ISecretResolver[] resolvers)
    {
        ArgumentNullException.ThrowIfNull(resolvers);
        _resolvers = resolvers;
    }

    /// <inheritdoc />
    public async Task<string> ResolveAsync(string scheme, string value, CancellationToken cancellationToken = default)
    {
        foreach (var r in _resolvers)
        {
            try
            {
                return await r.ResolveAsync(scheme, value, cancellationToken).ConfigureAwait(false);
            }
            catch (SecretSchemeUnsupportedException)
            {
                // Try the next resolver.
            }
        }
        throw new SecretSchemeUnsupportedException(
            $"no resolver registered for scheme '{scheme}'");
    }
}
