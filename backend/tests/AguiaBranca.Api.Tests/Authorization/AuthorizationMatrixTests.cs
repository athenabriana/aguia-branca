using System.Net;
using System.Net.Http.Json;
using AguiaBranca.Api.Controllers;
using AguiaBranca.Api.Tests.Support;
using AguiaBranca.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace AguiaBranca.Api.Tests.Authorization;

/// <summary>
/// B22 — matriz <b>endpoint × identidade</b> (anônimo, OPERADOR, GESTOR, LIDER) sobre a API real. Um teste de reflexão
/// falha se surgir (ou sumir) um endpoint sem entrada aqui, então nenhuma rota nova fica sem decisão de autorização.
/// Perfil sem permissão → 403; anônimo → 401 (exceto rotas públicas); perfil permitido → resposta "de negócio"
/// (200 nos GET sem id, 404 num id inexistente, 400/404/409/422 em escrita com corpo vazio) — nunca 401/403/5xx.
/// As escritas usam ids inexistentes e corpo vazio de propósito: a matriz não altera dados.
/// </summary>
[Trait("Category", "Integration")]
public sealed class AuthorizationMatrixTests(SeededApiFixture api) : IClassFixture<SeededApiFixture>
{
    private const string MissingId = "665f0000000000000000abcd";
    private static readonly Role[] Everyone = [Role.OPERADOR, Role.GESTOR, Role.LIDER];
    private static readonly int[] Rejected = [400, 404, 409, 422];

    private sealed record Ep(string Key, Role[]? Allowed, bool Public = false, int[]? Ok = null)
    {
        public string Method => Key.Split(' ')[0];
        public string Route => Key.Split(' ')[1];
        public string Path => "/" + Route.Replace("{id}", MissingId);
        public int[] Statuses => Ok ?? (Method == "GET" ? (Route.Contains("{id}") ? [404] : [200]) : Rejected);
    }

    // Fonte única de verdade: o contrato do spec (perfis por endpoint).
    private static readonly Ep[] Matrix =
    [
        new("POST api/v1/auth/login", null, Public: true, Ok: [400]),
        new("POST api/v1/auth/refresh", null, Public: true, Ok: [400]),
        new("POST api/v1/auth/logout", Everyone, Ok: [200, 204, 400]),
        new("GET api/v1/auth/me", Everyone),

        new("GET api/v1/guidelines", Everyone),
        new("GET api/v1/guidelines/history", Everyone),
        new("GET api/v1/guidelines/{id}", Everyone),
        new("POST api/v1/guidelines", [Role.LIDER]),
        new("PUT api/v1/guidelines/{id}", [Role.LIDER]),
        new("DELETE api/v1/guidelines/{id}", [Role.LIDER], Ok: [404]),

        new("GET api/v1/ideas", Everyone),
        new("GET api/v1/ideas/{id}", Everyone),
        new("POST api/v1/ideas", [Role.OPERADOR, Role.GESTOR]),
        new("PUT api/v1/ideas/{id}", Everyone),            // dono do recurso: regra por recurso (404/403), não por perfil
        new("DELETE api/v1/ideas/{id}", Everyone, Ok: [404]),
        new("PUT api/v1/ideas/{id}/ice", [Role.GESTOR]),
        new("POST api/v1/ideas/{id}/reject", [Role.GESTOR]),
        new("POST api/v1/ideas/{id}/approve", [Role.GESTOR], Ok: [404]),

        new("GET api/v1/projects", [Role.GESTOR, Role.LIDER]),
        new("GET api/v1/projects/{id}", [Role.GESTOR, Role.LIDER]),
        new("GET api/v1/projects/{id}/updates", [Role.GESTOR, Role.LIDER], Ok: [404]),
        new("POST api/v1/projects", [Role.GESTOR]),
        new("PUT api/v1/projects/{id}", [Role.GESTOR]),
        new("DELETE api/v1/projects/{id}", [Role.GESTOR], Ok: [404]),

        new("GET api/v1/users", [Role.GESTOR, Role.LIDER]),
        new("GET api/v1/users/ranking", Everyone),

        new("GET api/v1/reports/summary", [Role.LIDER]),
        new("GET api/v1/reports/guidelines", [Role.LIDER]),
        new("GET api/v1/reports/guidelines/{id}", [Role.LIDER]),
        new("GET api/v1/reports/projects/{id}", [Role.LIDER]),
        new("POST api/v1/reports/insights", [Role.LIDER], Ok: [200, 503]) // sem chave Gemini no teste: 503 AI_UNAVAILABLE é o "autorizado"
    ];

    public static IEnumerable<object?[]> Cases()
    {
        foreach (var ep in Matrix)
        {
            yield return [ep.Key, null];
            foreach (var role in Enum.GetValues<Role>()) yield return [ep.Key, role];
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Endpoint_AllowsOnlyTheContractedIdentities(string key, Role? identity)
    {
        var ep = Matrix.Single(e => e.Key == key);
        using var client = api.ClientAs(identity);
        using var request = new HttpRequestMessage(new HttpMethod(ep.Method), ep.Path);
        if (ep.Method is "POST" or "PUT") request.Content = JsonContent.Create(new { });

        var response = await client.SendAsync(request);
        var status = (int)response.StatusCode;
        var body = await response.Content.ReadAsStringAsync();

        if (identity is null)
        {
            if (ep.Public) status.Should().BeOneOf(ep.Statuses, $"{key} é público: {body}");
            else status.Should().Be(401, $"{key} sem token: {body}");
        }
        else if (ep.Public || ep.Allowed!.Contains(identity.Value))
            status.Should().BeOneOf(ep.Statuses, $"{key} como {identity}: {body}");
        else
            status.Should().Be(403, $"{key} como {identity}: {body}");
    }

    [Fact]
    public void EveryControllerEndpoint_HasAnEntryInTheMatrix_AndNoEntryIsStale()
    {
        var actual = api.Factory.Services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Where(a => a.ControllerTypeInfo.Namespace == typeof(AuthController).Namespace) // ignora os controllers de sonda dos testes
            .SelectMany(a => a.ActionConstraints!.OfType<Microsoft.AspNetCore.Mvc.ActionConstraints.HttpMethodActionConstraint>()
                .SelectMany(c => c.HttpMethods).Distinct()
                .Select(method => $"{method} {Normalize(a.AttributeRouteInfo!.Template!)}"))
            .ToList();

        actual.Should().OnlyHaveUniqueItems();
        actual.Should().BeEquivalentTo(Matrix.Select(e => e.Key),
            "toda rota nova precisa de uma decisão explícita de autorização na matriz (e rotas removidas saem dela)");
    }

    private static string Normalize(string template) => template.ToLowerInvariant().Replace("{id:objectid}", "{id}");
}
