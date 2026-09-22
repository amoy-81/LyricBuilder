using LyricBuilder.Abstractions.Extensions;
using LyricBuilder.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LyricBuilder.Core;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// The single composition point for the application layer. The host calls this and nothing else.
    /// </summary>
    public static IServiceCollection AddCore(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddLyricBuilderDatabase(configuration)
            .AddScoped<RequestContext>()
            // Every class in this assembly named "…Service" is registered against its interfaces.
            // Types named outside that convention must be added explicitly below.
            .RegisterByNamingConvention(typeof(RequestContext).Assembly, "Service");

        return services;
    }
}
