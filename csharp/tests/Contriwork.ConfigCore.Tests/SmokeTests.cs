using Xunit;

namespace Contriwork.ConfigCore.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void Public_Surface_Is_Reachable()
    {
        Assert.True(typeof(ISource).IsPublic && typeof(ISource).IsInterface);
        Assert.True(typeof(ISecretResolver).IsPublic && typeof(ISecretResolver).IsInterface);
        Assert.True(typeof(ISchemaAdapter<>).IsPublic && typeof(ISchemaAdapter<>).IsInterface);
        Assert.True(typeof(ConfigLoader).IsPublic && typeof(ConfigLoader).IsAbstract && typeof(ConfigLoader).IsSealed);
        Assert.True(typeof(EnvSource).IsPublic);
        Assert.True(typeof(FileSource).IsPublic);
        Assert.True(typeof(InMemorySource).IsPublic);
        Assert.True(typeof(EnvResolver).IsPublic);
        Assert.True(typeof(FileResolver).IsPublic);
        Assert.True(typeof(ChainResolver).IsPublic);
    }

    [Fact]
    public void V0_Placeholder_Is_Removed()
    {
        var asm = typeof(ConfigLoader).Assembly;
        Assert.Null(asm.GetType("Contriwork.ConfigCore.IConfigCorePort"));
    }
}
