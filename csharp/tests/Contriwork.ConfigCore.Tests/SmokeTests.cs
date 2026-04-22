using Xunit;

namespace Contriwork.ConfigCore.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void Port_Interface_Is_Public()
    {
        var t = typeof(IConfigCorePort);
        Assert.True(t.IsPublic);
        Assert.True(t.IsInterface);
    }

    [Fact]
    public void Port_Declares_Example_Method()
    {
        var method = typeof(IConfigCorePort).GetMethod(nameof(IConfigCorePort.ExampleAsync));
        Assert.NotNull(method);
    }
}
