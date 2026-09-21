using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AguiaBranca.Api.Tests.Support;

namespace AguiaBranca.Api.Tests.Infrastructure;

public sealed class ErrorHandlingTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    private readonly HttpClient _anonymous;

    public ErrorHandlingTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
        // Autenticado: a fallback policy responde 401 a anônimos até para rotas inexistentes/métodos errados.
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestJwt.Forge("665f00000000000000000001"));
        _anonymous = factory.CreateClient();
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private async Task<(HttpResponseMessage Response, JsonElement Body)> Get(string url)
    {
        var response = await _client.GetAsync(url);
        return (response, await Json(response));
    }

    [Fact]
    public async Task UnhandledException_Returns500ProblemJson_WithoutLeakingDetails()
    {
        var (response, body) = await Get("/api/v1/testprobe/boom");
        var raw = body.GetRawText();

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        body.GetProperty("code").GetString().Should().Be("INTERNAL_ERROR");
        body.GetProperty("status").GetInt32().Should().Be(500);
        body.GetProperty("traceId").GetString().Should().Be(response.Headers.GetValues("X-Correlation-ID").Single());
        raw.Should().NotContain("segredo-interno-123").And.NotContain("InvalidOperationException").And.NotContain("   at ");
    }

    [Theory]
    [InlineData("VALIDATION_ERROR", 400)]
    [InlineData("IDEA_NOT_EDITABLE", 409)]
    [InlineData("IDEA_INVALID_STATE", 409)]
    [InlineData("CONCURRENCY_CONFLICT", 409)]
    [InlineData("SELF_APPROVAL_FORBIDDEN", 403)]
    [InlineData("REGRA_DESCONHECIDA", 422)]
    public async Task DomainException_IsMappedByCode(string code, int status)
    {
        var (response, body) = await Get($"/api/v1/testprobe/domain/{code}");

        ((int)response.StatusCode).Should().Be(status);
        body.GetProperty("code").GetString().Should().Be(code == "VALIDATION_ERROR" ? "VALIDATION_ERROR" : code);
        body.GetProperty("errors").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task FluentValidationException_Returns400_WithCamelCaseFields()
    {
        var (response, body) = await Get("/api/v1/testprobe/validation");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = body.GetProperty("errors").EnumerateArray().ToList();
        errors.Select(e => e.GetProperty("field").GetString()).Should().BeEquivalentTo(["ice.impact", "title"]);
        errors.Should().OnlyContain(e => e.GetProperty("code").GetString() == "VALIDATION_ERROR");
    }

    [Theory]
    [InlineData("duplicate", 409, "CONFLICT")]
    [InlineData("concurrency", 409, "CONCURRENCY_CONFLICT")]
    [InlineData("unauthorized-access", 403, "FORBIDDEN")]
    [InlineData("payload-too-large", 413, "PAYLOAD_TOO_LARGE")]
    public async Task InfrastructureExceptions_AreTranslated(string route, int status, string code)
    {
        var (response, body) = await Get($"/api/v1/testprobe/{route}");

        ((int)response.StatusCode).Should().Be(status);
        body.GetProperty("code").GetString().Should().Be(code);
    }

    [Fact]
    public async Task UnknownRoute_Returns404ProblemJson_WithTraceId()
    {
        var (response, body) = await Get("/api/v1/nao-existe");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        body.GetProperty("code").GetString().Should().Be("RESOURCE_NOT_FOUND");
        body.GetProperty("instance").GetString().Should().Be("/api/v1/nao-existe");
        body.GetProperty("traceId").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("type").GetString().Should().Be("urn:aguiabranca:error:resource-not-found");
    }

    [Fact]
    public async Task WrongMethod_Returns405ProblemJson()
    {
        var response = await _client.DeleteAsync("/api/v1/testprobe/boom");
        var body = await Json(response);

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        body.GetProperty("code").GetString().Should().Be("METHOD_NOT_ALLOWED");
    }

    [Theory]
    [InlineData("nao-e-objectid", 404)]
    [InlineData("123", 404)]
    [InlineData("665f00000000000000000001", 200)]
    public async Task ObjectIdRouteConstraint_InvalidIds_NeverReachTheAction(string id, int status)
    {
        var response = await _client.GetAsync($"/api/v1/testprobe/items/{id}");
        ((int)response.StatusCode).Should().Be(status);
    }

    [Fact]
    public async Task ModelValidation_Returns400_InApiFormat()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/testprobe/echo", new { name = "nome-longo-demais" });
        var body = await Json(response);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        body.GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
        body.GetProperty("errors")[0].GetProperty("field").GetString().Should().Be("name");
    }

    [Fact]
    public async Task ModelValidation_MissingRequiredField_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/testprobe/echo", new { });
        (await Json(response)).GetProperty("errors")[0].GetProperty("field").GetString().Should().Be("name");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MalformedJson_AndInvalidEnum_Return400_WithoutInternals()
    {
        var malformed = await _client.PostAsync("/api/v1/testprobe/echo",
            new StringContent("{ nome: ", System.Text.Encoding.UTF8, "application/json"));
        malformed.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var badEnum = await _client.PostAsync("/api/v1/testprobe/echo",
            new StringContent("""{"name":"ok","status":"INEXISTENTE"}""", System.Text.Encoding.UTF8, "application/json"));
        badEnum.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await badEnum.Content.ReadAsStringAsync()).Should().NotContain("System.Text.Json").And.NotContain("Path:");
    }

    [Fact]
    public async Task UnsupportedContentType_Returns415ProblemJson()
    {
        var response = await _client.PostAsync("/api/v1/testprobe/echo", new StringContent("x", System.Text.Encoding.UTF8, "text/plain"));
        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
        (await Json(response)).GetProperty("code").GetString().Should().Be("UNSUPPORTED_MEDIA_TYPE");
    }

    [Theory]
    [InlineData("notfound", 404, "RESOURCE_NOT_FOUND")]
    [InlineData("forbidden", 403, "SELF_APPROVAL_FORBIDDEN")]
    [InlineData("conflict", 409, "IDEA_NOT_EDITABLE")]
    [InlineData("unprocessable", 422, "GUIDELINE_NOT_FOUND")]
    [InlineData("ai", 503, "AI_UNAVAILABLE")]
    public async Task ResultFailures_MapToHttpStatusAndCode(string kind, int status, string code)
    {
        var (response, body) = await Get($"/api/v1/testprobe/result/{kind}");

        ((int)response.StatusCode).Should().Be(status);
        body.GetProperty("code").GetString().Should().Be(code);
        body.GetProperty("errors")[0].GetProperty("code").GetString().Should().Be(code);
    }

    [Fact]
    public async Task ResultWithFieldError_KeepsFieldInErrors()
    {
        var (_, body) = await Get("/api/v1/testprobe/result/unprocessable");
        body.GetProperty("errors")[0].GetProperty("field").GetString().Should().Be("guidelineId");
    }

    [Fact]
    public async Task MultipleValidationErrors_ProduceSingle400_WithAllErrors()
    {
        var (response, body) = await Get("/api/v1/testprobe/result/multi");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("field").GetString()).Should().Equal("a", "b");
    }

    [Fact]
    public async Task ResultSuccess_Returns200_AndNoContentVariant204()
    {
        (await _client.GetAsync("/api/v1/testprobe/result/ok")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.GetAsync("/api/v1/testprobe/result/whatever")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Json_UsesCamelCase_StringEnums_KeepsNulls_AndUtcDates()
    {
        var (_, body) = await Get("/api/v1/testprobe/shapes");

        body.GetProperty("status").GetString().Should().Be("EM_ANALISE");
        body.GetProperty("division").GetString().Should().Be("LOGISTICA");
        body.GetProperty("optionalNote").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("createdAt").GetString().Should().Be("2026-09-21T12:00:00Z");
        body.GetProperty("investment").GetDecimal().Should().Be(1234.5m);
    }

    [Fact]
    public async Task CurrentUser_WhenAnonymous_IsNotAuthenticated()
    {
        var body = await Json(await _anonymous.GetAsync("/api/v1/testprobe/me"));
        body.GetProperty("isAuthenticated").GetBoolean().Should().BeFalse();
        body.GetProperty("id").GetString().Should().BeEmpty();
    }
}
