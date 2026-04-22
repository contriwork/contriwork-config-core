using Xunit;

namespace Contriwork.ConfigCore.Tests;

public sealed class ErrorsTests
{
    [Theory]
    [InlineData(typeof(ValidationFailedException), "VALIDATION_FAILED")]
    [InlineData(typeof(SourceUnavailableException), "SOURCE_UNAVAILABLE")]
    [InlineData(typeof(SourceParseFailedException), "SOURCE_PARSE_FAILED")]
    [InlineData(typeof(SecretRefMalformedException), "SECRET_REF_MALFORMED")]
    [InlineData(typeof(SecretSchemeUnsupportedException), "SECRET_SCHEME_UNSUPPORTED")]
    [InlineData(typeof(SecretRefUnresolvedException), "SECRET_REF_UNRESOLVED")]
    public void Code_Is_Stable(System.Type exceptionType, string expectedCode)
    {
        var instance = (ConfigException)System.Activator.CreateInstance(exceptionType, "boom")!;
        Assert.Equal(expectedCode, instance.Code);
        Assert.IsAssignableFrom<ConfigException>(instance);
    }

    [Fact]
    public void Details_Is_Optional()
    {
        var err = new ValidationFailedException("boom");
        Assert.Null(err.Details);
    }

    [Fact]
    public void Details_Can_Round_Trip()
    {
        var payload = new[] { new { Field = "x", Message = "required" } };
        var err = new ValidationFailedException("boom") { Details = payload };
        Assert.Same(payload, err.Details);
    }
}
