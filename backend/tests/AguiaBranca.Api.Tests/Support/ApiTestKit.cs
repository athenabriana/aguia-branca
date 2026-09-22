using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace AguiaBranca.Api.Tests.Support;

/// <summary>Atalhos para testes de API sobre o banco semeado (usuários novos, ideias, pontos).</summary>
internal static class ApiTestKit
{
    public static string Unique() => Guid.NewGuid().ToString("N")[..8];

    public static async Task<JsonElement> Json(HttpResponseMessage r) =>
        JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    /// <summary>Operador novo (sem pontos nem badges), já autenticado.</summary>
    public static async Task<HttpClient> NewUserClientAsync(SeededApiFixture api, Role role = Role.OPERADOR, string name = "Operador Novo")
    {
        var email = $"u-{Unique()}@aguiabranca.com";
        using (var scope = api.Factory.Services.CreateScope())
        {
            var user = AppUser.Create(name, email, role, Division.PASSAGEIROS, DateTime.UtcNow);
            (await scope.ServiceProvider.GetRequiredService<IIdentityService>().CreateUserAsync(user, SeededApiFixture.Password, default)).IsSuccess.Should().BeTrue();
        }
        var login = await Json(await api.Anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email, password = SeededApiFixture.Password }));
        var client = api.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.GetProperty("accessToken").GetString());
        return client;
    }

    public static async Task<JsonElement> Me(HttpClient client) => await Json(await client.GetAsync("/api/v1/auth/me"));
    public static async Task<int> Points(HttpClient client) => (await Me(client)).GetProperty("points").GetInt32();
    public static async Task<string[]> Badges(HttpClient client) =>
        (await Me(client)).GetProperty("badges").EnumerateArray().Select(b => b.GetString()!).ToArray();

    public static async Task<string> AnyGuidelineIdAsync(SeededApiFixture api)
    {
        using var lider = api.ClientAs(Role.LIDER);
        return (await Json(await lider.GetAsync("/api/v1/guidelines?pageSize=1"))).GetProperty("items")[0].GetProperty("id").GetString()!;
    }

    public static async Task<JsonElement> CreateIdeaAsync(HttpClient client, string? guidelineId = null, string? title = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/ideas", new
        {
            title = title ?? $"Ideia {Unique()}", description = "Descrição", category = "tecnologia", division = "LOGISTICA", guidelineId
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await Json(response);
    }

    public static async Task<JsonElement> GetIdeaAsync(HttpClient client, string id) =>
        await Json(await client.GetAsync($"/api/v1/ideas/{id}"));
}
