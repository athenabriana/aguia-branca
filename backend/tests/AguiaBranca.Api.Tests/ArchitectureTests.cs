using System.Reflection;
using AguiaBranca.Application;

namespace AguiaBranca.Api.Tests;

/// <summary>
/// Garante as regras de dependência entre camadas (design §3):
/// Domain → ∅ · Application → Domain · Infrastructure → Application, Domain.
/// </summary>
public class ArchitectureTests
{
    private static readonly string[] Layers =
        ["AguiaBranca.Domain", "AguiaBranca.Application", "AguiaBranca.Infrastructure", "AguiaBranca.Api"];

    private static IReadOnlyCollection<string> ReferencedLayers(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => Layers.Contains(n))
            .ToArray();

    [Fact]
    public void Domain_ShouldNotReferenceOtherLayers() =>
        ReferencedLayers(typeof(AguiaBranca.Domain.DomainMarker).Assembly).Should().BeEmpty();

    [Fact]
    public void Application_ShouldNotReferenceInfrastructureOrApi() =>
        ReferencedLayers(typeof(ApplicationMarker).Assembly)
            .Should().NotContain(["AguiaBranca.Infrastructure", "AguiaBranca.Api"]);

    [Fact]
    public void Infrastructure_ShouldNotReferenceApi() =>
        ReferencedLayers(typeof(AguiaBranca.Infrastructure.InfrastructureMarker).Assembly)
            .Should().NotContain("AguiaBranca.Api");

    [Fact]
    public void Domain_ShouldNotDependOnFrameworkPackages()
    {
        var forbidden = new[] { "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "MongoDB", "Microsoft.Extensions.Identity" };
        var refs = typeof(AguiaBranca.Domain.DomainMarker).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name!);

        refs.Where(n => forbidden.Any(n.StartsWith)).Should().BeEmpty();
    }
}
