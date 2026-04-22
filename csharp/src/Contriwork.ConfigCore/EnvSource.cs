using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Contriwork.ConfigCore;

/// <summary>
/// Read the process environment and map flat keys to a nested dictionary.
/// Keys starting with the configured prefix (case-sensitive) are collected,
/// the prefix is stripped, the remainder is lower-cased, and each occurrence
/// of the separator becomes a nesting level.
/// </summary>
public sealed class EnvSource : ISource
{
    private readonly string _prefix;
    private readonly string _separator;

    /// <summary>Initializes a new source.</summary>
    /// <param name="prefix">Only env keys starting with this are considered. Default: empty (all keys).</param>
    /// <param name="separator">Split delimiter within the stripped key. Default: <c>__</c>.</param>
    public EnvSource(string prefix = "", string separator = "__")
    {
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(separator);
        if (separator.Length == 0)
        {
            throw new ArgumentException("separator must be a non-empty string", nameof(separator));
        }
        _prefix = prefix;
        _separator = separator;
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, object?>> SnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, object?>();
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            var rawKey = (string)entry.Key;
            if (!rawKey.StartsWith(_prefix, StringComparison.Ordinal))
            {
                continue;
            }
            var stripped = rawKey[_prefix.Length..];
            if (stripped.Length == 0)
            {
                continue;
            }
            var path = stripped.ToLowerInvariant().Split(_separator, StringSplitOptions.None);
            var value = entry.Value as string ?? string.Empty;
            SetNested(result, path, value);
        }
        return Task.FromResult<IReadOnlyDictionary<string, object?>>(result);
    }

    private static void SetNested(
        Dictionary<string, object?> target,
        string[] path,
        string value)
    {
        var cursor = target;
        for (var i = 0; i < path.Length - 1; i++)
        {
            if (cursor.TryGetValue(path[i], out var existing)
                && existing is Dictionary<string, object?> nested)
            {
                cursor = nested;
            }
            else
            {
                var next = new Dictionary<string, object?>();
                cursor[path[i]] = next;
                cursor = next;
            }
        }
        cursor[path[^1]] = value;
    }
}
