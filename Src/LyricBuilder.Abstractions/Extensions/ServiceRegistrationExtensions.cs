using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace LyricBuilder.Abstractions.Extensions;

public static class ServiceRegistrationExtensions
{
    /// <summary>
    /// Scans <paramref name="assembly"/> for concrete classes whose name ends with
    /// <paramref name="suffix"/> and registers each against every interface it implements that
    /// lives in the same assembly. Keeps <c>AddCore</c> from growing one line per service.
    /// </summary>
    /// <remarks>
    /// A type only gets picked up if it follows the naming convention — anything named outside it
    /// (a <c>…Guard</c>, a <c>…Notifier</c>) must still be registered explicitly.
    /// </remarks>
    public static IServiceCollection RegisterByNamingConvention(
        this IServiceCollection services,
        Assembly assembly,
        string suffix = "Service",
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        var implementations = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
            .Where(t => t.Name.EndsWith(suffix, StringComparison.Ordinal));

        foreach (var implementation in implementations)
        {
            var contracts = implementation.GetInterfaces()
                .Where(i => i.Assembly == assembly)
                .ToList();

            if (contracts.Count == 0)
            {
                services.Add(new ServiceDescriptor(implementation, implementation, lifetime));
                continue;
            }

            // Register the concrete type once, then point every interface at that same
            // registration so a service resolved by two contracts stays one instance per scope.
            services.Add(new ServiceDescriptor(implementation, implementation, lifetime));
            foreach (var contract in contracts)
                services.Add(new ServiceDescriptor(contract, sp => sp.GetRequiredService(implementation), lifetime));
        }

        return services;
    }
}
