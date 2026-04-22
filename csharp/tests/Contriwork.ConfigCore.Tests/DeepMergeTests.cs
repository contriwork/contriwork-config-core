using System.Collections.Generic;
using Xunit;

namespace Contriwork.ConfigCore.Tests;

public sealed class DeepMergeTests
{
    // Use a small helper to invoke internal DeepMerge via a test-only reflection shim.
    // The src project exposes InternalsVisibleTo via Tests.csproj's InternalsVisibleTo attribute
    // on Contriwork.ConfigCore (added via an InternalsVisibleTo using System.Runtime.CompilerServices).
    // We instead run through the public load_config for behaviour; leave direct DeepMerge
    // coverage implicit via ConfigLoaderTests.

    [Fact]
    public async System.Threading.Tasks.Task Overlay_Overrides_Earlier_Via_Loader()
    {
        var baseData = new Dictionary<string, object?>
        {
            ["db_url"] = "sqlite://default",
            ["debug"] = false,
        };
        var overlayData = new Dictionary<string, object?>
        {
            ["debug"] = true,
        };
        var cfg = await ConfigLoader.LoadConfigAsync(
            new DataAnnotationsAdapter<AppConfig>(),
            new ISource[]
            {
                new InMemorySource(baseData),
                new InMemorySource(overlayData),
            },
            resolver: null);
        Assert.Equal("sqlite://default", cfg.DbUrl);
        Assert.True(cfg.Debug);
    }

    [Fact]
    public async System.Threading.Tasks.Task Nested_Dicts_Merge_Key_Wise()
    {
        var baseData = new Dictionary<string, object?>
        {
            ["db"] = new Dictionary<string, object?>
            {
                ["url"] = "sqlite://x",
                ["pool"] = (long)5,
            },
        };
        var overlayData = new Dictionary<string, object?>
        {
            ["db"] = new Dictionary<string, object?>
            {
                ["pool"] = (long)10,
            },
        };
        var cfg = await ConfigLoader.LoadConfigAsync(
            new DataAnnotationsAdapter<NestedConfig>(),
            new ISource[]
            {
                new InMemorySource(baseData),
                new InMemorySource(overlayData),
            },
            resolver: null);
        Assert.Equal("sqlite://x", cfg.Db!.Url);
        Assert.Equal(10, cfg.Db!.Pool);
    }

    public sealed class AppConfig
    {
        [System.ComponentModel.DataAnnotations.Required]
        public string DbUrl { get; set; } = string.Empty;

        public bool Debug { get; set; }
    }

    public sealed class NestedConfig
    {
        public DbSection? Db { get; set; }

        public sealed class DbSection
        {
            public string Url { get; set; } = string.Empty;

            public int Pool { get; set; }
        }
    }
}
