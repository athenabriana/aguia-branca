using AguiaBranca.Application.Common.Validation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace AguiaBranca.Application.Tests.Common;

public sealed record SampleRequest(string Title, SampleNested Nested);
public sealed record SampleNested(int Impact);

public sealed class SampleRequestValidator : AbstractValidator<SampleRequest>
{
    public SampleRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Título é obrigatório.");
        RuleFor(x => x.Nested.Impact).InclusiveBetween(1, 10).WithMessage("Impacto deve estar entre 1 e 10.");
    }
}

public class ValidationServiceTests
{
    private static ServiceProvider Provider(bool withValidator = true)
    {
        var services = new ServiceCollection();
        if (withValidator) services.AddScoped<IValidator<SampleRequest>, SampleRequestValidator>();
        services.AddApplication();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task ValidRequest_HasNoErrors()
    {
        using var sp = Provider();
        var errors = await sp.GetRequiredService<IValidationService>()
            .ValidateAsync(new SampleRequest("ok", new SampleNested(5)), default);
        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task InvalidRequest_ReturnsFieldErrors_InCamelCasePaths()
    {
        using var sp = Provider();
        var errors = await sp.GetRequiredService<IValidationService>()
            .ValidateAsync(new SampleRequest("", new SampleNested(11)), default);

        errors.Should().HaveCount(2);
        errors.Should().Contain(e => e.Field == "title" && e.Code == "VALIDATION_ERROR" && e.Message == "Título é obrigatório.");
        errors.Should().Contain(e => e.Field == "nested.impact");
    }

    [Fact]
    public async Task WithoutRegisteredValidator_IsConsideredValid()
    {
        using var sp = Provider(withValidator: false);
        var errors = await sp.GetRequiredService<IValidationService>()
            .ValidateAsync(new SampleRequest("", new SampleNested(0)), default);
        errors.Should().BeEmpty();
    }

    [Fact]
    public void AddApplication_RegistersValidationService()
    {
        var services = new ServiceCollection().AddApplication().BuildServiceProvider();
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetService<IValidationService>().Should().NotBeNull();
    }
}
