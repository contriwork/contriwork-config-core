using System.Collections.Generic;

namespace Contriwork.ConfigCore;

/// <summary>
/// Internal deep-merge. Dicts recurse; lists and scalars replace; neither input
/// is mutated. See CONTRACT.md §Behavior/Precedence and merging.
/// </summary>
internal static class DeepMerge
{
    public static Dictionary<string, object?> Merge(
        IReadOnlyDictionary<string, object?> @base,
        IReadOnlyDictionary<string, object?> overlay)
    {
        var result = new Dictionary<string, object?>(@base);
        foreach (var kv in overlay)
        {
            if (result.TryGetValue(kv.Key, out var baseVal)
                && baseVal is IReadOnlyDictionary<string, object?> baseDict
                && kv.Value is IReadOnlyDictionary<string, object?> overlayDict)
            {
                result[kv.Key] = Merge(baseDict, overlayDict);
            }
            else
            {
                result[kv.Key] = kv.Value;
            }
        }
        return result;
    }
}
