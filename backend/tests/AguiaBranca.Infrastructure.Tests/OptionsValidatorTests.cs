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

    [Theory]
    [InlineData(255, false)]
    [InlineData(256, true)]
    [InlineData(8192, true)]
    [InlineData(8193, false)]
    public void Gemini_MaxOutputTokens_IsBounded(int tokens, bool ok) =>
        new GeminiOptionsValidator().Validate(null, new GeminiOptions { MaxOutputTokens = tokens }).Succeeded.Should().Be(ok);

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(10_000, true)]
    [InlineData(10_001, false)]
    public void Gemini_RetryDelay_IsBounded(int delay, bool ok) =>
        new GeminiOptionsValidator().Validate(null, new GeminiOptions { RetryDelayMilliseconds = delay }).Succeeded.Should().Be(ok);

    [Theory]
    [InlineData("", true)]
    [InlineData("minimal", true)]
    [InlineData("High", true)]
    [InlineData("extreme", false)]
    public void Gemini_ThinkingLevel_IsRestrictedToKnownValues(string level, bool ok) =>
        new GeminiOptionsValidator().Validate(null, new GeminiOptions { ThinkingLevel = level }).Succeeded.Should().Be(ok);

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

    [Theory]
    [InlineData("https://painel.exemplo.com", true)]
    [InlineData("http://localhost:3000", true)]
    [InlineData("*", false)]
    [InlineData("https://painel.exemplo.com/", false)]
    [InlineData("https://painel.exemplo.com/app", false)]
    [InlineData("painel.exemplo.com", false)]
    [InlineData("ftp://painel.exemplo.com", false)]
    public void Cors_Origins_MustBeExactOrigins(string origin, bool ok) =>
        new CorsOptionsValidator().Validate(null, new CorsOptions { Origins = [origin] }).Succeeded.Should().Be(ok);

    [Fact]
    public void Cors_NoOrigins_IsValid() =>
        new CorsOptionsValidator().Validate(null, new CorsOptions()).Succeeded.Should().BeTrue();

    [Theory]
    [InlineData(1023, false)]
    [InlineData(1024, true)]
    [InlineData(1_048_576, true)]
    [InlineData(10_485_760, true)]
    [InlineData(10_485_761, false)]
    public void Security_MaxRequestBody_IsBounded(long bytes, bool ok) =>
        new SecurityOptionsValidator().Validate(null, new SecurityOptions { MaxRequestBodyBytes = bytes }).Succeeded.Should().Be(ok);

    [Theory]
    [InlineData(0, false)]
    [InlineData(365, true)]
    [InlineData(731, false)]
    public void Security_HstsMaxAge_IsBounded(int days, bool ok) =>
        new SecurityOptionsValidator().Validate(null, new SecurityOptions { HstsMaxAgeDays = days }).Succeeded.Should().Be(ok);
}
