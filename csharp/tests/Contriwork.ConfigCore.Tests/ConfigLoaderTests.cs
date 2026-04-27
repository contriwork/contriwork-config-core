using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Contriwork.ConfigCore.Tests;

public sealed class ConfigLoaderTests
{
    public sealed class AppConfig
    {
        [Required]
        public string DbUrl { get; set; } = string.Empty;

        public bool Debug { get; set; }
    }

    [Fact]
    public async Task Single_Source_RoundTrip()
    {
        var cfg = await ConfigLoader.LoadConfigAsync(
            new DataAnnotationsAdapter<AppConfig>(),
            new ISource[]
            {
                new InMemorySource(new Dictionary<string, object?>
                {
                    ["db_url"] = "sqlite://x",
                    ["debug"] = true,
                }),
            },
            resolver: null);
        Assert.Equal("sqlite://x", cfg.DbUrl);
        Assert.True(cfg.Debug);
    }

    [Fact]
    public async Task Later_Source_Overrides_Earlier()
    {
        var cfg = await ConfigLoader.LoadConfigAsync(
            new DataAnnotationsAdapter<AppConfig>(),
            new ISource[]
            {
                new InMemorySource(new Dictionary<string, object?>
                {
                    ["db_url"] = "sqlite://default",
                    ["debug"] = false,
                }),
                new InMemorySource(new Dictionary<string, object?>
                {
                    ["debug"] = true,
                }),
            },
            resolver: null);
        Assert.Equal("sqlite://default", cfg.DbUrl);
        Assert.True(cfg.Debug);
    }

    [Fact]
    public async Task Empty_Sources_Raises()
    {
        await Assert.ThrowsAsync<ValidationFailedException>(() =>
            ConfigLoader.LoadConfigAsync<AppConfig>(
                new DataAnnotationsAdapter<AppConfig>(),
                System.Array.Empty<ISource>(),
                resolver: null));
    }

    [Fact]
    public async Task Default_Resolver_Is_Env()
    {
        var key = $"TEST_CCC_{System.Guid.NewGuid():N}";
        System.Environment.SetEnvironmentVariable(key, "pg://from-env");
        try
        {
            var cfg = await ConfigLoader.LoadConfigAsync(
                new DataAnnotationsAdapter<AppConfig>(),
                new ISource[]
                {
                    new InMemorySource(new Dictionary<string, object?>
                    {
                        ["db_url"] = $"${{env:{key}}}",
                    }),
                });
            Assert.Equal("pg://from-env", cfg.DbUrl);
        }
        finally
        {
            System.Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public async Task Explicit_Null_Resolver_Passes_Refs_Through()
    {
        // resolver: null is mapped to NullResolver internally; refs pass through verbatim.
        var cfg = await ConfigLoader.LoadConfigAsync(
            new DataAnnotationsAdapter<AppConfig>(),
            new ISource[]
            {
                new InMemorySource(new Dictionary<string, object?>
                {
                    ["db_url"] = "${env:NOT_INTERPOLATED}",
                }),
            },
            resolver: null);
        Assert.Equal("${env:NOT_INTERPOLATED}", cfg.DbUrl);
    }

    [Fact]
    public async Task Explicit_NullResolver_Passes_Refs_Verbatim()
    {
        var cfg = await ConfigLoader.LoadConfigAsync(
            new DataAnnotationsAdapter<AppConfig>(),
            new ISource[]
            {
                new InMemorySource(new Dictionary<string, object?>
                {
                    ["db_url"] = "${env:NOT_INTERPOLATED}",
                }),
            },
            resolver: new NullResolver());
        Assert.Equal("${env:NOT_INTERPOLATED}", cfg.DbUrl);
    }

    [Fact]
    public async Task File_Plus_Env_Merges()
    {
        var path = Path.GetTempFileName() + ".yaml";
        await File.WriteAllTextAsync(path, "db_url: sqlite://file-default\ndebug: false\n");
        var key = $"TEST_CCC_{System.Guid.NewGuid():N}_";
        System.Environment.SetEnvironmentVariable(key + "DEBUG", "true");
        try
        {
            var cfg = await ConfigLoader.LoadConfigAsync(
                new DataAnnotationsAdapter<AppConfig>(),
                new ISource[]
                {
                    new FileSource(path),
                    new EnvSource(prefix: key),
                },
                resolver: null);
            Assert.Equal("sqlite://file-default", cfg.DbUrl);
            Assert.True(cfg.Debug);
        }
        finally
        {
            File.Delete(path);
            System.Environment.SetEnvironmentVariable(key + "DEBUG", null);
        }
    }

    [Fact]
    public async Task Unresolved_Secret_Propagates()
    {
        var key = $"TEST_CCC_{System.Guid.NewGuid():N}";
        await Assert.ThrowsAsync<SecretRefUnresolvedException>(() =>
            ConfigLoader.LoadConfigAsync(
                new DataAnnotationsAdapter<AppConfig>(),
                new ISource[]
                {
                    new InMemorySource(new Dictionary<string, object?>
                    {
                        ["db_url"] = $"${{env:{key}}}",
                    }),
                },
                new EnvResolver()));
    }
}
