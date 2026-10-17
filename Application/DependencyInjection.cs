using MarwanQuTech.Application.Common.Behaviors;
using MarwanQuTech.Application.Common.Caching;
using Microsoft.Extensions.DependencyInjection;

namespace MarwanQuTech.Application;

public static class DependencyInjection
{
    /// <summary>Registers MediatR handlers, pipeline behaviors and validators from this assembly.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddScoped<IPublicContentInvalidator, PublicContentInvalidator>();

        return services;
    }
}
