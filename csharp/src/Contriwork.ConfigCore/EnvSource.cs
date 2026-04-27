using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Contriwork.ConfigCore;

/// <summary>
/// Categories of JSON literal that <see cref="EnvSource"/> may decode when its
/// <c>decodeJsonFor</c> opt-in flag enables them. Mirrors the Python
/// <c>JsonCategory</c> Literal and the TypeScript <c>JsonCategory</c> union;
/// enum member names are kept identical to the cross-language string values
/// (<c>"list"</c>, <c>"dict"</c>, <c>"bool"</c>, <c>"int"</c>, <c>"float"</c>)
/// so contract fixtures read the same in all three languages.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming",
    "CA1720:Identifiers should not contain type names",
    Justification = "Enum members mirror the JSON-fixture category names (list/dict/bool/int/float) for cross-language parity with Python and TypeScript.")]
public enum JsonCategory
{
    /// <summary>JSON array → <see cref="List{T}"/> of <c>object?</c>.</summary>
    List,

    /// <summary>JSON object → <see cref="Dictionary{TKey,TValue}"/> of <c>string</c> → <c>object?</c>.</summary>
    Dict,

    /// <summary>JSON <c>true</c> / <c>false</c> → <see cref="bool"/>.</summary>
    Bool,

    /// <summary>JSON integer that fits in <see cref="long"/> → <see cref="long"/>.</summary>
    Int,

    /// <summary>JSON number with a fractional part → <see cref="double"/>.</summary>
    Float,
}

/// <summary>
/// Read the process environment and map flat keys to a nested dictionary.
/// Keys starting with the configured prefix (case-sensitive) are collected,
/// the prefix is stripped, the remainder is lower-cased, and each occurrence
/// of the separator becomes a nesting level.
///
/// Values are returned as strings by default; coercion is the schema
/// adapter's job. Passing <c>decodeJsonFor</c> with one or more
/// <see cref="JsonCategory"/> values opts into best-effort
/// <c>JsonDocument.Parse</c> decoding — the parsed value replaces the raw
/// string only when its JSON kind matches one of the listed categories;
/// otherwise the raw string is kept (the schema validator decides).
/// </summary>
public sealed class EnvSource : ISource
{
    private readonly string _prefix;
    private readonly string _separator;
    private readonly HashSet<JsonCategory> _decodeJsonFor;

    /// <summary>Initializes a new source.</summary>
    /// <param name="prefix">Only env keys starting with this are considered. Default: empty (all keys).</param>
    /// <param name="separator">Split delimiter within the stripped key. Default: <c>__</c>.</param>
    /// <param name="decodeJsonFor">
    /// Optional opt-in JSON decode. Default: <c>null</c> (no decoding —
    /// preserves v0.1.0 behaviour). When supplied, each env value is
    /// best-effort parsed with <see cref="JsonDocument"/>; the parsed value
    /// replaces the raw string only when its category is in this set.
    /// </param>
    public EnvSource(
        string prefix = "",
        string separator = "__",
        IReadOnlyCollection<JsonCategory>? decodeJsonFor = null)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(separator);
        if (separator.Length == 0)
        {
            throw new ArgumentException("separator must be a non-empty string", nameof(separator));
        }
        _prefix = prefix;
        _separator = separator;
        _decodeJsonFor = decodeJsonFor is null
            ? new HashSet<JsonCategory>()
            : new HashSet<JsonCategory>(decodeJsonFor);
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
            var raw = entry.Value as string ?? string.Empty;
            var decoded = MaybeDecode(raw);
            SetNested(result, path, decoded);
        }
        return Task.FromResult<IReadOnlyDictionary<string, object?>>(result);
    }

    private object? MaybeDecode(string raw)
    {
        if (_decodeJsonFor.Count == 0)
        {
            return raw;
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(raw);
        }
        catch (JsonException)
        {
            return raw;
        }

        try
        {
            var parsed = NormalizeJson(doc.RootElement, out var category);
            return category is { } c && _decodeJsonFor.Contains(c) ? parsed : raw;
        }
        finally
        {
            doc.Dispose();
        }
    }

    private static object? NormalizeJson(JsonElement el, out JsonCategory? category)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                {
                    category = JsonCategory.Dict;
                    var dict = new Dictionary<string, object?>();
                    foreach (var p in el.EnumerateObject())
                    {
                        dict[p.Name] = NormalizeJson(p.Value, out _);
                    }
                    return dict;
                }
            case JsonValueKind.Array:
                {
                    category = JsonCategory.List;
                    return el.EnumerateArray().Select(e => NormalizeJson(e, out _)).ToList();
                }
            case JsonValueKind.True:
                category = JsonCategory.Bool;
                return true;
            case JsonValueKind.False:
                category = JsonCategory.Bool;
                return false;
            case JsonValueKind.Number:
                if (el.TryGetInt64(out var l))
                {
                    category = JsonCategory.Int;
                    return l;
                }
                category = JsonCategory.Float;
                return el.GetDouble();
            case JsonValueKind.String:
                category = null;
                return el.GetString();
            case JsonValueKind.Null:
                category = null;
                return null;
            default:
                category = null;
                return null;
        }
    }

    private static void SetNested(
        Dictionary<string, object?> target,
        string[] path,
        object? value)
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
