using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Contriwork.ConfigCore.Tests;

public sealed class RefResolverTests
{
    private sealed class FakeResolver : ISecretResolver
    {
        private readonly Dictionary<(string scheme, string value), string> _table;

        public FakeResolver(Dictionary<(string scheme, string value), string> table)
        {
            _table = table;
        }

        public Task<string> ResolveAsync(string scheme, string value, CancellationToken cancellationToken = default)
        {
            if (_table.TryGetValue((scheme, value), out var resolved))
            {
                return Task.FromResult(resolved);
            }
            throw new SecretSchemeUnsupportedException($"not in table: {scheme}:{value}");
        }
    }

    [Fact]
    public async Task Single_Ref_Is_Resolved_Via_Loader()
    {
        var resolver = new FakeResolver(new() { [("env", "FOO")] = "bar" });
        var cfg = await ConfigLoader.LoadConfigAsync(
            new DataAnnotationsAdapter<RefConfig>(),
            new ISource[]
            {
                new InMemorySource(new Dictionary<string, object?> { ["value"] = "${env:FOO}" }),
            },
            resolver);
        Assert.Equal("bar", cfg.Value);
    }

    [Fact]
    public async Task Multiple_Refs_In_One_String()
    {
        var resolver = new FakeResolver(new()
        {
            [("env", "USER")] = "alice",
            [("env", "HOST")] = "h.example",
        });
        var cfg = await ConfigLoader.LoadConfigAsync(
            new DataAnnotationsAdapter<RefConfig>(),
            new ISource[]
            {
                new InMemorySource(new Dictionary<string, object?> { ["value"] = "${env:USER}@${env:HOST}" }),
            },
            resolver);
        Assert.Equal("alice@h.example", cfg.Value);
    }

    [Fact]
    public async Task Double_Brace_Escape_Yields_Literal()
    {
        var resolver = new FakeResolver(new()
        {
            [("env", "X")] = "should-not-appear",
        });
        var cfg = await ConfigLoader.LoadConfigAsync(
            new DataAnnotationsAdapter<RefConfig>(),
            new ISource[]
            {
                new InMemorySource(new Dictionary<string, object?> { ["value"] = "$${env:X}" }),
            },
            resolver);
        Assert.Equal("${env:X}", cfg.Value);
    }

    [Fact]
    public async Task Malformed_Unclosed_Brace_Raises()
    {
        var resolver = new FakeResolver(new());
        await Assert.ThrowsAsync<SecretRefMalformedException>(() =>
            ConfigLoader.LoadConfigAsync(
                new DataAnnotationsAdapter<RefConfig>(),
                new ISource[]
                {
                    new InMemorySource(new Dictionary<string, object?> { ["value"] = "${env:FOO" }),
                },
                resolver));
    }

    [Fact]
    public async Task Number_Passes_Through_Unchanged()
    {
        var resolver = new FakeResolver(new());
        var cfg = await ConfigLoader.LoadConfigAsync(
            new DataAnnotationsAdapter<NumericConfig>(),
            new ISource[]
            {
                new InMemorySource(new Dictionary<string, object?> { ["value"] = (long)42 }),
            },
            resolver);
        Assert.Equal(42, cfg.Value);
    }

    public sealed class RefConfig
    {
        public string Value { get; set; } = string.Empty;
    }

    public sealed class NumericConfig
    {
        public int Value { get; set; }
    }
}
