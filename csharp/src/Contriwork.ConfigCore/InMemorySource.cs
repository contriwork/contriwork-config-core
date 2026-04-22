using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Contriwork.ConfigCore;

/// <summary>A source backed by a dictionary. Primarily for tests.</summary>
public sealed class InMemorySource : ISource
{
    private readonly IReadOnlyDictionary<string, object?> _data;

    /// <summary>Initializes a new source from the given data.</summary>
    public InMemorySource(IReadOnlyDictionary<string, object?> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        _data = data;
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, object?>> SnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        var copy = new Dictionary<string, object?>(_data);
        return Task.FromResult<IReadOnlyDictionary<string, object?>>(copy);
    }
}
