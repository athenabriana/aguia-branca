using System.Reflection;
using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Validation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace AguiaBranca.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(ApplicationMarker).Assembly;
        services.AddHandlersFrom(assembly);
        services.AddValidatorsFromAssembly(assembly, ServiceLifetime.Singleton, includeInternalTypes: true);
        services.AddScoped<IValidationService, ValidationService>();
        return services;
    }

    /// <summary>
    /// Registra cada <see cref="IHandler{TRequest,TResponse}"/> concreto como ele mesmo (injetado nos controllers)
    /// e como a interface fechada. Sem mediator: um handler por caso de uso.
    /// </summary>
    public static IServiceCollection AddHandlersFrom(this IServiceCollection services, Assembly assembly)
    {
        var handlerInterface = typeof(IHandler<,>);

        foreach (var type in assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false }))
        {
            var closed = type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterface)
                .ToArray();
            if (closed.Length == 0) continue;

            services.AddScoped(type);
            foreach (var @interface in closed)
                services.AddScoped(@interface, sp => sp.GetRequiredService(type));
        }

        return services;
    }
}
