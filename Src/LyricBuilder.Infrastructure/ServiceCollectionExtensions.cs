using LyricBuilder.Abstractions.Persistence;
using LyricBuilder.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LyricBuilder.Infrastructure;

public static class ServiceCollectionExtensions
{
    public const string ConnectionStringName = "LyricBuilderDatabase";

    /// <summary>
    /// Registers the DbContext, the open-generic repository, and the unit of work.
    /// </summary>
    public static IServiceCollection AddLyricBuilderDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                $"ConnectionStrings:{ConnectionStringName} is not configured.");

        services.AddDbContext<LyricBuilderDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<LyricBuilderDbContext>());

        return services;
    }
}
