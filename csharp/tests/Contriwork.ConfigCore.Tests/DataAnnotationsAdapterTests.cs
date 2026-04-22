using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace Contriwork.ConfigCore.Tests;

public sealed class DataAnnotationsAdapterTests
{
    public sealed class AppConfig
    {
        [Required]
        public string DbUrl { get; set; } = string.Empty;

        public bool Debug { get; set; }

        [Range(1, 100)]
        public int PoolSize { get; set; } = 1;
    }

    [Fact]
    public void Valid_Data_Returns_Typed_Instance()
    {
        var cfg = new DataAnnotationsAdapter<AppConfig>().Validate(new Dictionary<string, object?>
        {
            ["db_url"] = "sqlite://x",
            ["debug"] = true,
            ["pool_size"] = (long)5,
        });
        Assert.Equal("sqlite://x", cfg.DbUrl);
        Assert.True(cfg.Debug);
        Assert.Equal(5, cfg.PoolSize);
    }

    [Fact]
    public void Missing_Required_Throws_ValidationFailed()
    {
        var ex = Assert.Throws<ValidationFailedException>(() =>
            new DataAnnotationsAdapter<AppConfig>().Validate(new Dictionary<string, object?>
            {
                ["debug"] = true,
                ["pool_size"] = (long)5,
            }));
        Assert.Equal("VALIDATION_FAILED", ex.Code);
        Assert.NotNull(ex.Details);
    }

    [Fact]
    public void Constraint_Violation_Throws()
    {
        Assert.Throws<ValidationFailedException>(() =>
            new DataAnnotationsAdapter<AppConfig>().Validate(new Dictionary<string, object?>
            {
                ["db_url"] = "x",
                ["debug"] = false,
                ["pool_size"] = (long)0, // out of [1, 100]
            }));
    }

    [Fact]
    public void String_Number_Coerces()
    {
        var cfg = new DataAnnotationsAdapter<AppConfig>().Validate(new Dictionary<string, object?>
        {
            ["db_url"] = "x",
            ["debug"] = false,
            ["pool_size"] = "42",
        });
        Assert.Equal(42, cfg.PoolSize);
    }

    [Fact]
    public void String_Bool_Coerces()
    {
        var cfg = new DataAnnotationsAdapter<AppConfig>().Validate(new Dictionary<string, object?>
        {
            ["db_url"] = "x",
            ["debug"] = "true",
            ["pool_size"] = (long)1,
        });
        Assert.True(cfg.Debug);
    }
}
