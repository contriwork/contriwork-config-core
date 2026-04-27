using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Contriwork.ConfigCore;

/// <summary>
/// Public entry point for v1 contract. See CONTRACT.md §Methods/<c>load_config</c>.
///
/// Order of operations:
/// 1. <see cref="ISource.SnapshotAsync"/> for each source, in declared order.
/// 2. Deep-merge — later sources override earlier ones.
/// 3. Walk string leaves and resolve <c>${scheme:value}</c> via the resolver.
/// 4. Validate via the <see cref="ISchemaAdapter{T}"/>.
/// </summary>
public static class ConfigLoader
{
    /// <summary>
    /// Load, merge, resolve, and validate configuration.
    /// </summary>
    /// <param name="schema">Adapter that validates the merged dict into <typeparamref name="T"/>.</param>
    /// <param name="sources">Ordered list of <see cref="ISource"/> instances; later sources override earlier ones.</param>
    /// <param name="resolver">
    /// Optional <see cref="ISecretResolver"/> for <c>${...}</c> refs. Pass
    /// <c>null</c> to disable secret resolution entirely — this is mapped
    /// to <see cref="NullResolver"/> internally so the resolution path
    /// stays uniform; semantically the two are equivalent. If omitted
    /// (use <see cref="LoadConfigAsync{T}(ISchemaAdapter{T}, IEnumerable{ISource}, CancellationToken)"/>)
    /// the default <see cref="EnvResolver"/> is used.
    /// </param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>An instance of <typeparamref name="T"/>.</returns>
    /// <exception cref="ValidationFailedException">
    /// When <paramref name="sources"/> is empty, or the final dict fails schema validation.
    /// </exception>
    public static async Task<T> LoadConfigAsync<T>(
        ISchemaAdapter<T> schema,
        IEnumerable<ISource> sources,
        ISecretResolver? resolver,
        CancellationToken cancellationToken = default)
    {
        System.ArgumentNullException.ThrowIfNull(schema);
        System.ArgumentNullException.ThrowIfNull(sources);

        var sourceList = new List<ISource>(sources);
        if (sourceList.Count == 0)
        {
            throw new ValidationFailedException("LoadConfigAsync requires at least one source");
        }

        var merged = new Dictionary<string, object?>();
        foreach (var source in sourceList)
        {
            var snapshot = await source.SnapshotAsync(cancellationToken).ConfigureAwait(false);
            merged = DeepMerge.Merge(merged, snapshot);
        }

        // Map explicit null to NullResolver so the call site reads
        // self-documentingly when introspected from logs / tests.
        var effectiveResolver = resolver ?? new NullResolver();
        var resolved = await RefResolver.ResolveAsync(merged, effectiveResolver, cancellationToken).ConfigureAwait(false);

        if (resolved is not IReadOnlyDictionary<string, object?> resolvedDict)
        {
            // After merge, the root is always a dict.
            resolvedDict = new Dictionary<string, object?>();
        }

        return schema.Validate(resolvedDict);
    }

    /// <summary>
    /// Load with the default <see cref="EnvResolver"/>. See
    /// <see cref="LoadConfigAsync{T}(ISchemaAdapter{T}, IEnumerable{ISource}, ISecretResolver?, CancellationToken)"/>.
    /// </summary>
    public static Task<T> LoadConfigAsync<T>(
        ISchemaAdapter<T> schema,
        IEnumerable<ISource> sources,
        CancellationToken cancellationToken = default)
    {
        return LoadConfigAsync(schema, sources, new EnvResolver(), cancellationToken);
    }
}
