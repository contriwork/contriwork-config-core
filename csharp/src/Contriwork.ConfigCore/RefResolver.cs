using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Contriwork.ConfigCore;

/// <summary>
/// Internal walker for <c>${scheme:value}</c> refs inside a parsed config tree.
/// Mirrors the Python <c>_refs.resolve_refs</c> and TypeScript <c>resolveRefs</c>
/// implementations — any semantic drift is a contract bug.
/// </summary>
internal static class RefResolver
{
    private const string EscapeSentinel = "\x00CCC_ESCAPED_BRACE_\x00";

    private static readonly Regex RefPattern = new(
        @"\$\{([a-z][a-z0-9_]*):([^}]*)\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static async Task<object?> ResolveAsync(
        object? data,
        ISecretResolver? resolver,
        CancellationToken cancellationToken)
    {
        if (resolver is null)
        {
            return data;
        }

        return data switch
        {
            IReadOnlyDictionary<string, object?> dict => await ResolveDictAsync(dict, resolver, cancellationToken).ConfigureAwait(false),
            IReadOnlyList<object?> list => await ResolveListAsync(list, resolver, cancellationToken).ConfigureAwait(false),
            string text => await ResolveStringAsync(text, resolver, cancellationToken).ConfigureAwait(false),
            _ => data,
        };
    }

    private static async Task<Dictionary<string, object?>> ResolveDictAsync(
        IReadOnlyDictionary<string, object?> dict,
        ISecretResolver resolver,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, object?>(dict.Count);
        foreach (var kv in dict)
        {
            result[kv.Key] = await ResolveAsync(kv.Value, resolver, cancellationToken).ConfigureAwait(false);
        }
        return result;
    }

    private static async Task<List<object?>> ResolveListAsync(
        IReadOnlyList<object?> list,
        ISecretResolver resolver,
        CancellationToken cancellationToken)
    {
        var result = new List<object?>(list.Count);
        foreach (var item in list)
        {
            result.Add(await ResolveAsync(item, resolver, cancellationToken).ConfigureAwait(false));
        }
        return result;
    }

    private static async Task<string> ResolveStringAsync(
        string text,
        ISecretResolver resolver,
        CancellationToken cancellationToken)
    {
        var protectedText = text.Replace("$${", EscapeSentinel, System.StringComparison.Ordinal);
        AssertNoMalformed(protectedText, text);

        var parts = new List<string>();
        var lastEnd = 0;
        foreach (Match m in RefPattern.Matches(protectedText))
        {
            var scheme = m.Groups[1].Value;
            var value = m.Groups[2].Value;
            var resolved = await resolver.ResolveAsync(scheme, value, cancellationToken).ConfigureAwait(false);
            parts.Add(protectedText[lastEnd..m.Index]);
            parts.Add(resolved);
            lastEnd = m.Index + m.Length;
        }
        parts.Add(protectedText[lastEnd..]);

        return string.Concat(parts).Replace(EscapeSentinel, "${", System.StringComparison.Ordinal);
    }

    private static void AssertNoMalformed(string protectedText, string original)
    {
        var idx = 0;
        while (true)
        {
            var start = protectedText.IndexOf("${", idx, System.StringComparison.Ordinal);
            if (start == -1)
            {
                return;
            }
            var match = RefPattern.Match(protectedText, start);
            if (!match.Success || match.Index != start)
            {
                throw new SecretRefMalformedException(
                    $"malformed secret reference at offset {start} in '{original}'");
            }
            idx = match.Index + match.Length;
        }
    }
}
