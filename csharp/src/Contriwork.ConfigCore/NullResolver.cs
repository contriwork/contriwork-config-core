using System.Threading;
using System.Threading.Tasks;

namespace Contriwork.ConfigCore;

/// <summary>
/// Explicit opt-out — every <c>${scheme:value}</c> is returned verbatim.
///
/// Use this when secret resolution is not appropriate for the load path
/// and the call site should read self-documentingly:
///
/// <code>
/// await ConfigLoader.LoadConfigAsync(schema, sources, new NullResolver());
/// </code>
///
/// is equivalent in semantics to passing <c>null</c> for <c>resolver</c>
/// (refs pass through as literal strings) but names the intent at the
/// call site. Mirrors the Python <c>NullResolver</c> class and the
/// TypeScript <c>NullResolver</c> class.
/// </summary>
public sealed class NullResolver : ISecretResolver
{
    /// <inheritdoc />
    public Task<string> ResolveAsync(string scheme, string value, CancellationToken cancellationToken = default)
    {
        return Task.FromResult($"${{{scheme}:{value}}}");
    }
}
