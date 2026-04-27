using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Contriwork.ConfigCore.Tests;

public sealed class NullResolverTests
{
    [Fact]
    public async Task NullResolver_Returns_Ref_Verbatim()
    {
        var r = new NullResolver();
        Assert.Equal("${env:DB_URL}", await r.ResolveAsync("env", "DB_URL"));
        Assert.Equal("${vault:secret/data/x}", await r.ResolveAsync("vault", "secret/data/x"));
    }

    [Fact]
    public async Task NullResolver_Accepts_Any_Scheme()
    {
        var r = new NullResolver();
        Assert.Equal("${anything:anyvalue}", await r.ResolveAsync("anything", "anyvalue"));
    }

    [Fact]
    public async Task NullResolver_Handles_Empty_Value()
    {
        var r = new NullResolver();
        Assert.Equal("${env:}", await r.ResolveAsync("env", string.Empty));
    }
}

public sealed class ResolversTests
{
    // ── EnvResolver ─────────────────────────────────────────────────

    [Fact]
    public async Task Env_Resolves()
    {
        var key = $"TEST_CCC_{System.Guid.NewGuid():N}";
        System.Environment.SetEnvironmentVariable(key, "s3cr3t");
        try
        {
            Assert.Equal("s3cr3t", await new EnvResolver().ResolveAsync("env", key));
        }
        finally
        {
            System.Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public async Task Env_Missing_Variable_Raises()
    {
        var key = $"TEST_CCC_{System.Guid.NewGuid():N}";
        await Assert.ThrowsAsync<SecretRefUnresolvedException>(() =>
            new EnvResolver().ResolveAsync("env", key));
    }

    [Fact]
    public async Task Env_Empty_Name_Raises()
    {
        await Assert.ThrowsAsync<SecretRefUnresolvedException>(() =>
            new EnvResolver().ResolveAsync("env", string.Empty));
    }

    [Fact]
    public async Task Env_Wrong_Scheme_Raises()
    {
        await Assert.ThrowsAsync<SecretSchemeUnsupportedException>(() =>
            new EnvResolver().ResolveAsync("file", "whatever"));
    }

    // ── FileResolver ────────────────────────────────────────────────

    [Fact]
    public async Task File_Resolves_Absolute()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "mysecret\n");
        try
        {
            Assert.Equal("mysecret", await new FileResolver().ResolveAsync("file", path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task File_Strips_Trailing_Whitespace_Only()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "  value with spaces  \n\n");
        try
        {
            Assert.Equal("  value with spaces", await new FileResolver().ResolveAsync("file", path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task File_Missing_Raises()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nope-{System.Guid.NewGuid():N}");
        await Assert.ThrowsAsync<SecretRefUnresolvedException>(() =>
            new FileResolver().ResolveAsync("file", path));
    }

    [Fact]
    public async Task File_Empty_Value_Raises()
    {
        await Assert.ThrowsAsync<SecretRefUnresolvedException>(() =>
            new FileResolver().ResolveAsync("file", string.Empty));
    }

    [Fact]
    public async Task File_Wrong_Scheme_Raises()
    {
        await Assert.ThrowsAsync<SecretSchemeUnsupportedException>(() =>
            new FileResolver().ResolveAsync("env", "whatever"));
    }

    [Fact]
    public async Task File_Relative_With_BaseDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ccc-{System.Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "s.txt");
        await File.WriteAllTextAsync(file, "relative-secret\n");
        try
        {
            var r = new FileResolver(baseDir: dir);
            Assert.Equal("relative-secret", await r.ResolveAsync("file", "s.txt"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ── ChainResolver ───────────────────────────────────────────────

    [Fact]
    public async Task Chain_First_Hit_Wins()
    {
        var key = $"TEST_CCC_{System.Guid.NewGuid():N}";
        System.Environment.SetEnvironmentVariable(key, "env-value");
        try
        {
            var chain = new ChainResolver(new EnvResolver(), new FileResolver());
            Assert.Equal("env-value", await chain.ResolveAsync("env", key));
        }
        finally
        {
            System.Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public async Task Chain_Skips_Unsupported_To_Next()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "chained\n");
        try
        {
            var chain = new ChainResolver(new EnvResolver(), new FileResolver());
            Assert.Equal("chained", await chain.ResolveAsync("file", path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Chain_No_Handler_Raises()
    {
        var chain = new ChainResolver(new EnvResolver(), new FileResolver());
        await Assert.ThrowsAsync<SecretSchemeUnsupportedException>(() =>
            chain.ResolveAsync("vault", "some/path"));
    }

    [Fact]
    public async Task Chain_Propagates_Unresolved()
    {
        var key = $"TEST_CCC_{System.Guid.NewGuid():N}";
        var chain = new ChainResolver(new EnvResolver(), new FileResolver());
        await Assert.ThrowsAsync<SecretRefUnresolvedException>(() =>
            chain.ResolveAsync("env", key));
    }

    [Fact]
    public async Task Chain_Empty_Raises()
    {
        await Assert.ThrowsAsync<SecretSchemeUnsupportedException>(() =>
            new ChainResolver().ResolveAsync("env", "X"));
    }
}
