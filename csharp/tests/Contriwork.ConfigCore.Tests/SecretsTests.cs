using System;
using Xunit;

namespace Contriwork.ConfigCore.Tests;

public sealed class SecretsTests
{
    // ── SecretStrOrEmpty ────────────────────────────────────────────

    [Fact]
    public void OrEmpty_Returns_Value_When_Set()
    {
        Assert.Equal("hunter2", Secrets.SecretStrOrEmpty("hunter2"));
    }

    [Fact]
    public void OrEmpty_Returns_Empty_String_When_Null()
    {
        Assert.Equal(string.Empty, Secrets.SecretStrOrEmpty(null));
    }

    [Fact]
    public void OrEmpty_Returns_Empty_String_When_Empty()
    {
        // An empty string is a legal value — it is *not* coerced to null.
        Assert.Equal(string.Empty, Secrets.SecretStrOrEmpty(string.Empty));
    }

    // ── SecretStrRequired ───────────────────────────────────────────

    [Fact]
    public void Required_Returns_Value_When_Set()
    {
        Assert.Equal("hunter2", Secrets.SecretStrRequired("hunter2", "db_password"));
    }

    [Fact]
    public void Required_Throws_With_Field_Name_When_Null()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => Secrets.SecretStrRequired(null, "db_password"));
        Assert.Contains("db_password", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Required_Rejects_Empty_Field_Name()
    {
        Assert.Throws<ArgumentException>(
            () => Secrets.SecretStrRequired("hunter2", string.Empty));
    }
}
