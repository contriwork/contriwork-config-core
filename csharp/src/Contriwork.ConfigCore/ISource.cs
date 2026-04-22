using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Contriwork.ConfigCore;

/// <summary>
/// A source of configuration keys. Cross-language: Python <c>Source</c>
/// protocol, TypeScript <c>Source</c> interface.
/// </summary>
public interface ISource
{
    /// <summary>
    /// Return a fresh snapshot of this source's current keys, as a nested
    /// dictionary. Raises <see cref="SourceUnavailableException"/> or
    /// <see cref="SourceParseFailedException"/> per CONTRACT §Error Taxonomy.
    /// </summary>
    Task<IReadOnlyDictionary<string, object?>> SnapshotAsync(
        CancellationToken cancellationToken = default);
}
