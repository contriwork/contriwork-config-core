using System.Threading;
using System.Threading.Tasks;

namespace Contriwork.ConfigCore;

/// <summary>
/// Resolve a <c>${scheme:value}</c> reference to a plain string. Cross-language:
/// Python <c>SecretResolver</c> protocol, TypeScript <c>SecretResolver</c>
/// interface.
/// </summary>
public interface ISecretResolver
{
    /// <summary>
    /// Resolve a reference or throw.
    /// </summary>
    /// <exception cref="SecretSchemeUnsupportedException">
    /// This resolver does not handle <paramref name="scheme"/>.
    /// <see cref="ChainResolver"/> relies on this signal to try the next resolver.
    /// </exception>
    /// <exception cref="SecretRefUnresolvedException">
    /// <paramref name="scheme"/> is handled but <paramref name="value"/> cannot be
    /// produced (env var not set, file missing, etc.).
    /// </exception>
    Task<string> ResolveAsync(string scheme, string value, CancellationToken cancellationToken = default);
}
