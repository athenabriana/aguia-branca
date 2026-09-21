using System.Net;
using System.Text.Json;
using AguiaBranca.Api.Tests.Support;
using AguiaBranca.Infrastructure.Tests.Support;
using MongoDB.Driver;

namespace AguiaBranca.Api.Tests.Infrastructure;

public class HealthWithoutMongoTests
{
    private static ApiFactory Unreachable() =>
        new ApiFactory().With("ConnectionStrings:Mongo", "mongodb://127.0.0.1:1/?directConnection=true&serverSelectionTimeoutMS=500&connectTimeoutMS=500");

    [Fact]
    public async Task Live_IsHealthy_EvenWithoutMongo()
    {
        using var factory = Unreachable();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("status").GetString().Should().Be("Healthy");
        body.GetProperty("checks").GetArrayLength().Should().Be(0);
    }

    [Theory]
    [InlineData("/health/ready")]
    [InlineData("/health")]
    public async Task ReadyAndAggregate_Return503_WhenMongoIsDown_WithoutLeakingDetails(string path)
    {
        using var factory = Unreachable();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);
        var raw = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        JsonDocument.Parse(raw).RootElement.GetProperty("status").GetString().Should().Be("Unhealthy");
        raw.Should().NotContain("127.0.0.1").And.NotContain("Timeout").And.NotContain("MongoDB.Driver");
    }
}

[Collection(MongoCollection.Name)]
[Trait("Category", "Integration")]
public sealed class HealthWithMongoTests(MongoFixture fixture) : IAsyncLifetime
{
    private TestDatabase _db = null!;
    public async Task InitializeAsync() => _db = await fixture.CreateDatabaseAsync(withIndexes: false);
    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    [InlineData("/health")]
    public async Task AllEndpoints_AreHealthy_WithMongo(string path)
    {
        using var factory = new ApiFactory().WithMongo(fixture.ConnectionString, _db.Name);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("status").GetString().Should().Be("Healthy");
        if (path != "/health/live") body.GetProperty("checks")[0].GetProperty("name").GetString().Should().Be("mongodb");
    }

    [Fact]
    public async Task Startup_WithMongo_CreatesIndexes()
    {
        using var factory = new ApiFactory().WithMongo(fixture.ConnectionString, _db.Name);
        using var client = factory.CreateClient(); // dispara o startup (MongoInitializer)

        var indexes = await (await _db.Raw("projects").Indexes.ListAsync()).ToListAsync();
        indexes.Select(i => i["name"].AsString).Should().Contain("ux_projects_originatingIdeaId");
    }
}

public class SwaggerTests
{
    [Fact]
    public async Task Development_ExposesSwagger_WithBearerScheme()
    {
        using var factory = new ApiFactory { Environment = "Development" };
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var scheme = doc.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        scheme.GetProperty("scheme").GetString().Should().Be("bearer");
        scheme.GetProperty("bearerFormat").GetString().Should().Be("JWT");
        doc.GetProperty("info").GetProperty("version").GetString().Should().Be("v1");
    }

    [Fact]
    public async Task Production_HidesSwagger_ByDefault()
    {
        using var factory = new ApiFactory { Environment = "Production" };
        using var client = factory.CreateClient();

        // Sem o Swagger a URL não existe; a fallback policy responde 401 a anônimos (não revela nem a rota).
        (await client.GetAsync("/swagger/v1/swagger.json")).StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
        (await client.GetAsync("/swagger/v1/swagger.json")).StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Production_CanEnableSwagger_ByConfiguration()
    {
        using var factory = new ApiFactory { Environment = "Production" }.With("Swagger:Enabled", "true");
        using var client = factory.CreateClient();

        (await client.GetAsync("/swagger/v1/swagger.json")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Development_CanDisableSwagger_ByConfiguration()
    {
        using var factory = new ApiFactory { Environment = "Development" }.With("Swagger:Enabled", "false");
        using var client = factory.CreateClient();

        (await client.GetAsync("/swagger/v1/swagger.json")).StatusCode.Should().NotBe(HttpStatusCode.OK);
    }
}
