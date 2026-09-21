using AguiaBranca.Infrastructure.Configuration;

namespace AguiaBranca.Infrastructure.Tests;

public class OptionsValidatorTests
{
    private static JwtOptions ValidJwt() => new()
    {
        Issuer = "iss", Audience = "aud", Key = new string('k', 32), AccessMinutes = 30, RefreshDays = 7
    };

    [Fact]
    public void Jwt_Valid_Succeeds() =>
        new JwtOptionsValidator().Validate(null, ValidJwt()).Succeeded.Should().BeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("curta")]
    public void Jwt_MissingOrShortKey_Fails(string key)
    {
        var options = ValidJwt();
        options.Key = key;

        var result = new JwtOptionsValidator().Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().ContainSingle(f => f.Contains("Jwt:Key"));
    }

    [Fact]
    public void Jwt_KeyExactly32Bytes_Succeeds()
    {
        var options = ValidJwt();
        options.Key = new string('a', 32);
        new JwtOptionsValidator().Validate(null, options).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Jwt_KeyIsMeasuredInBytes_NotChars()
    {
        var options = ValidJwt();
        options.Key = new string('é', 16); // 16 chars, 32 bytes UTF-8
        new JwtOptionsValidator().Validate(null, options).Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5000)]
    public void Jwt_AccessMinutesOutOfRange_Fails(int minutes)
    {
        var options = ValidJwt();
        options.AccessMinutes = minutes;
        new JwtOptionsValidator().Validate(null, options).Failed.Should().BeTrue();
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("http://localhost", false)]
    [InlineData("mongodb://localhost:27017/?directConnection=true", true)]
    [InlineData("mongodb+srv://u:p@cluster.mongodb.net", true)]
    public void Mongo_ConnectionString_IsValidated(string cs, bool ok)
    {
        var result = new MongoOptionsValidator().Validate(null, new MongoOptions { ConnectionString = cs });
        result.Succeeded.Should().Be(ok);
    }

    [Fact]
    public void Gemini_WithoutKey_IsAllowed_AndNotConfigured()
    {
        var options = new GeminiOptions();
        new GeminiOptionsValidator().Validate(null, options).Succeeded.Should().BeTrue();
        options.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void Gemini_KeyWithoutModel_Fails()
    {
        var options = new GeminiOptions { ApiKey = "abc" };
        var result = new GeminiOptionsValidator().Validate(null, options);
        result.Failed.Should().BeTrue();
        result.Failures.Should().ContainSingle(f => f.Contains("Gemini:Model"));
    }

    [Fact]
    public void Gemini_KeyAndModel_IsConfigured()
    {
        var options = new GeminiOptions { ApiKey = "abc", Model = "some-model" };
        new GeminiOptionsValidator().Validate(null, options).Succeeded.Should().BeTrue();
        options.IsConfigured.Should().BeTrue();
    }

    [Fact]
    public void Gemini_NonHttpsBaseUrl_Fails()
    {
        var options = new GeminiOptions { BaseUrl = "http://insecure.example" };
        new GeminiOptionsValidator().Validate(null, options).Failed.Should().BeTrue();
    }

    [Fact]
    public void Reports_UnknownTimeZone_Fails() =>
        new ReportsOptionsValidator().Validate(null, new ReportsOptions { TimeZone = "Mars/Olympus" })
            .Failed.Should().BeTrue();

    [Fact]
    public void Reports_SaoPaulo_Succeeds() =>
        new ReportsOptionsValidator().Validate(null, new ReportsOptions()).Succeeded.Should().BeTrue();
}
