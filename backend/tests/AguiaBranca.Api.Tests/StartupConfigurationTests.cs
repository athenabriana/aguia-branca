using Microsoft.Extensions.Options;
using AguiaBranca.Api.Tests.Support;

namespace AguiaBranca.Api.Tests;

public class StartupConfigurationTests
{
    private static ApiFactory Factory(Action<ApiFactory>? tweak = null)
    {
        var factory = new ApiFactory();
        tweak?.Invoke(factory);
        return factory;
    }

    [Fact]
    public async Task Host_WithValidConfiguration_Starts()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/");

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public void Host_WithoutJwtKey_FailsToStart_WithClearMessage()
    {
        using var factory = Factory(f => f.With("Jwt:Key", ""));
        var act = () => factory.CreateClient();
        act.Should().Throw<OptionsValidationException>().WithMessage("*Jwt:Key*");
    }

    [Fact]
    public void Host_WithShortJwtKey_FailsToStart()
    {
        using var factory = Factory(f => f.With("Jwt:Key", "curta"));
        var act = () => factory.CreateClient();
        act.Should().Throw<OptionsValidationException>().WithMessage("*32 bytes*");
    }

    [Fact]
    public void Host_WithoutMongoConnection_FailsToStart()
    {
        using var factory = Factory(f => f.With("ConnectionStrings:Mongo", ""));
        var act = () => factory.CreateClient();
        act.Should().Throw<OptionsValidationException>().WithMessage("*ConnectionStrings:Mongo*");
    }
}
