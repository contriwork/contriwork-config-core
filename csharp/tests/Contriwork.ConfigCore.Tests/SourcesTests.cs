using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Contriwork.ConfigCore.Tests;

public sealed class SourcesTests
{
    // ── InMemorySource ──────────────────────────────────────────────

    [Fact]
    public async Task InMemory_Returns_Data()
    {
        var src = new InMemorySource(new Dictionary<string, object?>
        {
            ["a"] = (long)1,
            ["b"] = new Dictionary<string, object?> { ["c"] = (long)2 },
        });
        var snap = await src.SnapshotAsync();
        Assert.Equal((long)1, snap["a"]);
    }

    // ── EnvSource ───────────────────────────────────────────────────

    [Fact]
    public async Task Env_Filters_By_Prefix()
    {
        var key = $"TEST_CCC_{System.Guid.NewGuid():N}";
        System.Environment.SetEnvironmentVariable(key, "hit");
        try
        {
            var src = new EnvSource(prefix: key);
            var snap = await src.SnapshotAsync();
            Assert.Empty(snap); // prefix equals the key leaves nothing to map
        }
        finally
        {
            System.Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public async Task Env_Nested_Via_Separator()
    {
        var prefix = $"TEST_CCC_{System.Guid.NewGuid():N}_";
        System.Environment.SetEnvironmentVariable(prefix + "DB__URL", "sqlite://x");
        try
        {
            var src = new EnvSource(prefix: prefix);
            var snap = await src.SnapshotAsync();
            var db = Assert.IsType<Dictionary<string, object?>>(snap["db"]);
            Assert.Equal("sqlite://x", db["url"]);
        }
        finally
        {
            System.Environment.SetEnvironmentVariable(prefix + "DB__URL", null);
        }
    }

    [Fact]
    public async Task Env_Flat_Key_Without_Separator()
    {
        var prefix = $"TEST_CCC_{System.Guid.NewGuid():N}_";
        System.Environment.SetEnvironmentVariable(prefix + "DEBUG", "true");
        try
        {
            var src = new EnvSource(prefix: prefix);
            var snap = await src.SnapshotAsync();
            Assert.Equal("true", snap["debug"]);
        }
        finally
        {
            System.Environment.SetEnvironmentVariable(prefix + "DEBUG", null);
        }
    }

    [Fact]
    public void Env_Empty_Separator_Throws()
    {
        Assert.Throws<System.ArgumentException>(() => new EnvSource(separator: string.Empty));
    }

    // ── EnvSource: decodeJsonFor ────────────────────────────────────

    [Fact]
    public async Task Env_Decode_Json_List()
    {
        var prefix = $"TEST_CCC_{System.Guid.NewGuid():N}_";
        System.Environment.SetEnvironmentVariable(prefix + "HOSTS", "[\"a\", \"b\", \"c\"]");
        try
        {
            var src = new EnvSource(prefix: prefix, decodeJsonFor: new[] { JsonCategory.List });
            var snap = await src.SnapshotAsync();
            var hosts = Assert.IsType<List<object?>>(snap["hosts"]);
            Assert.Equal(new object?[] { "a", "b", "c" }, hosts);
        }
        finally
        {
            System.Environment.SetEnvironmentVariable(prefix + "HOSTS", null);
        }
    }

    [Fact]
    public async Task Env_Decode_Json_Dict()
    {
        var prefix = $"TEST_CCC_{System.Guid.NewGuid():N}_";
        System.Environment.SetEnvironmentVariable(prefix + "RATE_LIMITS", "{\"market_data\": 10}");
        try
        {
            var src = new EnvSource(prefix: prefix, decodeJsonFor: new[] { JsonCategory.Dict });
            var snap = await src.SnapshotAsync();
            var rl = Assert.IsType<Dictionary<string, object?>>(snap["rate_limits"]);
            Assert.Equal((long)10, rl["market_data"]);
        }
        finally
        {
            System.Environment.SetEnvironmentVariable(prefix + "RATE_LIMITS", null);
        }
    }

    [Fact]
    public async Task Env_Decode_Json_Off_Keeps_Raw_String()
    {
        var prefix = $"TEST_CCC_{System.Guid.NewGuid():N}_";
        System.Environment.SetEnvironmentVariable(prefix + "HOSTS", "[\"a\", \"b\"]");
        try
        {
            var src = new EnvSource(prefix: prefix);
            var snap = await src.SnapshotAsync();
            Assert.Equal("[\"a\", \"b\"]", snap["hosts"]);
        }
        finally
        {
            System.Environment.SetEnvironmentVariable(prefix + "HOSTS", null);
        }
    }

    [Fact]
    public async Task Env_Decode_Json_Invalid_Falls_Back_To_Raw()
    {
        var prefix = $"TEST_CCC_{System.Guid.NewGuid():N}_";
        System.Environment.SetEnvironmentVariable(prefix + "HOSTS", "not-json");
        try
        {
            var src = new EnvSource(prefix: prefix, decodeJsonFor: new[] { JsonCategory.List });
            var snap = await src.SnapshotAsync();
            Assert.Equal("not-json", snap["hosts"]);
        }
        finally
        {
            System.Environment.SetEnvironmentVariable(prefix + "HOSTS", null);
        }
    }

    [Fact]
    public async Task Env_Decode_Json_Wrong_Category_Falls_Back()
    {
        var prefix = $"TEST_CCC_{System.Guid.NewGuid():N}_";
        System.Environment.SetEnvironmentVariable(prefix + "DEBUG", "true");
        try
        {
            var src = new EnvSource(prefix: prefix, decodeJsonFor: new[] { JsonCategory.List });
            var snap = await src.SnapshotAsync();
            Assert.Equal("true", snap["debug"]);
        }
        finally
        {
            System.Environment.SetEnvironmentVariable(prefix + "DEBUG", null);
        }
    }

    [Fact]
    public async Task Env_Decode_Json_Bool_Int_Float()
    {
        var prefix = $"TEST_CCC_{System.Guid.NewGuid():N}_";
        System.Environment.SetEnvironmentVariable(prefix + "DEBUG", "true");
        System.Environment.SetEnvironmentVariable(prefix + "POOL", "10");
        System.Environment.SetEnvironmentVariable(prefix + "RATIO", "0.5");
        try
        {
            var src = new EnvSource(
                prefix: prefix,
                decodeJsonFor: new[] { JsonCategory.Bool, JsonCategory.Int, JsonCategory.Float });
            var snap = await src.SnapshotAsync();
            Assert.Equal(true, snap["debug"]);
            Assert.Equal((long)10, snap["pool"]);
            Assert.Equal(0.5, snap["ratio"]);
        }
        finally
        {
            System.Environment.SetEnvironmentVariable(prefix + "DEBUG", null);
            System.Environment.SetEnvironmentVariable(prefix + "POOL", null);
            System.Environment.SetEnvironmentVariable(prefix + "RATIO", null);
        }
    }

    // ── FileSource ──────────────────────────────────────────────────

    [Fact]
    public async Task File_Yaml()
    {
        var path = Path.GetTempFileName() + ".yaml";
        await File.WriteAllTextAsync(path, "db:\n  url: sqlite://yaml\n  pool: 10\n");
        try
        {
            var snap = await new FileSource(path).SnapshotAsync();
            var db = Assert.IsType<Dictionary<string, object?>>(snap["db"]);
            Assert.Equal("sqlite://yaml", db["url"]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task File_Json()
    {
        var path = Path.GetTempFileName() + ".json";
        await File.WriteAllTextAsync(path, "{\"db\": {\"url\": \"sqlite://json\"}}");
        try
        {
            var snap = await new FileSource(path).SnapshotAsync();
            var db = Assert.IsType<Dictionary<string, object?>>(snap["db"]);
            Assert.Equal("sqlite://json", db["url"]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task File_Toml()
    {
        var path = Path.GetTempFileName() + ".toml";
        await File.WriteAllTextAsync(path, "[db]\nurl = \"sqlite://toml\"\npool = 5\n");
        try
        {
            var snap = await new FileSource(path).SnapshotAsync();
            var db = Assert.IsType<Dictionary<string, object?>>(snap["db"]);
            Assert.Equal("sqlite://toml", db["url"]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task File_Missing_Required_Raises()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nope-{System.Guid.NewGuid():N}.yaml");
        await Assert.ThrowsAsync<SourceUnavailableException>(() => new FileSource(path).SnapshotAsync());
    }

    [Fact]
    public async Task File_Missing_Optional_Returns_Empty()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nope-{System.Guid.NewGuid():N}.yaml");
        var snap = await new FileSource(path, required: false).SnapshotAsync();
        Assert.Empty(snap);
    }

    [Fact]
    public void File_Unknown_Extension_Throws_At_Construction()
    {
        Assert.Throws<SourceParseFailedException>(() => new FileSource("cfg.bin"));
    }

    [Fact]
    public async Task File_Non_Mapping_Root_Raises()
    {
        var path = Path.GetTempFileName() + ".yaml";
        await File.WriteAllTextAsync(path, "- a\n- b\n");
        try
        {
            await Assert.ThrowsAsync<SourceParseFailedException>(() => new FileSource(path).SnapshotAsync());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task File_Format_Override()
    {
        var path = Path.GetTempFileName() + ".cfg";
        await File.WriteAllTextAsync(path, "{\"a\": 1}");
        try
        {
            var snap = await new FileSource(path, FileFormat.Json).SnapshotAsync();
            Assert.Equal((long)1, snap["a"]);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
