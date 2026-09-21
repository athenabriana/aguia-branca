using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;

namespace AguiaBranca.Api.Tests;

public class StartupConfigurationTests
{
    private static WebApplicationFactory<Program> FactoryWith(Dictionary<string, string?> settings) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            foreach (var (key, value) in settings) b.UseSetting(key, value);
        });

    private static Dictionary<string, string?> Valid() => new()
    {
        ["ConnectionStrings:Mongo"] = "mongodb://localhost:27017/?directConnection=true",
        ["Jwt:Key"] = new string('k', 48)
    };

    [Fact]
    public async Task Host_WithValidConfiguration_Starts()
    {
        using var factory = FactoryWith(Valid());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/");

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public void Host_WithoutJwtKey_FailsToStart_WithClearMessage()
    {
        var settings = Valid();
        settings["Jwt:Key"] = "";
        using var factory = FactoryWith(settings);

        var act = () => factory.CreateClient();

        act.Should().Throw<OptionsValidationException>().WithMessage("*Jwt:Key*");
    }

    [Fact]
    public void Host_WithShortJwtKey_FailsToStart()
    {
        var settings = Valid();
        settings["Jwt:Key"] = "curta";
        using var factory = FactoryWith(settings);

        var act = () => factory.CreateClient();

        act.Should().Throw<OptionsValidationException>().WithMessage("*32 bytes*");
    }

    [Fact]
    public void Host_WithoutMongoConnection_FailsToStart()
    {
        var settings = Valid();
        settings["ConnectionStrings:Mongo"] = "";
        using var factory = FactoryWith(settings);

        var act = () => factory.CreateClient();

        act.Should().Throw<OptionsValidationException>().WithMessage("*ConnectionStrings:Mongo*");
    }
}
