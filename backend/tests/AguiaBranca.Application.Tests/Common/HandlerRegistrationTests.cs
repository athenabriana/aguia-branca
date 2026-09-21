using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Results;
using Microsoft.Extensions.DependencyInjection;

namespace AguiaBranca.Application.Tests.Common;

public sealed record PingRequest(string Text);

public sealed class PingHandler : IHandler<PingRequest, string>
{
    public Task<Result<string>> HandleAsync(PingRequest request, CancellationToken ct) =>
        Task.FromResult(Result.Ok($"pong:{request.Text}"));
}

public abstract class AbstractPingHandler : IHandler<PingRequest, string>
{
    public abstract Task<Result<string>> HandleAsync(PingRequest request, CancellationToken ct);
}

public class HandlerRegistrationTests
{
    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddHandlersFrom(typeof(HandlerRegistrationTests).Assembly);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task ConcreteHandler_IsResolvable_AsItself_AndAsInterface()
    {
        using var sp = Build();
        using var scope = sp.CreateScope();

        var concrete = scope.ServiceProvider.GetRequiredService<PingHandler>();
        var byInterface = scope.ServiceProvider.GetRequiredService<IHandler<PingRequest, string>>();

        byInterface.Should().BeSameAs(concrete);
        (await concrete.HandleAsync(new PingRequest("x"), default)).Value.Should().Be("pong:x");
    }

    [Fact]
    public void AbstractHandlers_AreNotRegistered()
    {
        using var sp = Build();
        using var scope = sp.CreateScope();
        scope.ServiceProvider.GetService<AbstractPingHandler>().Should().BeNull();
    }

    [Fact]
    public void Handlers_AreScoped()
    {
        using var sp = Build();
        PingHandler a, b;
        using (var s1 = sp.CreateScope()) a = s1.ServiceProvider.GetRequiredService<PingHandler>();
        using (var s2 = sp.CreateScope()) b = s2.ServiceProvider.GetRequiredService<PingHandler>();
        a.Should().NotBeSameAs(b);
    }
}
