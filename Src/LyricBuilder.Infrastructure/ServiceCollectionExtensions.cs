using LyricBuilder.Abstractions.Persistence;
using LyricBuilder.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LyricBuilder.Infrastructure;

public static class ServiceCollectionExtensions
{
    public const string ConnectionStringName = "LyricBuilderDatabase";

    /// <summary>
    /// The URL-style connection string that hosting platforms inject for a managed database.
    /// </summary>
    public const string DatabaseUrlKey = "DATABASE_URL";

    /// <summary>
    /// Registers the DbContext, the open-generic repository, and the unit of work.
    /// </summary>
    public static IServiceCollection AddLyricBuilderDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = ResolveConnectionString(configuration);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                $"ConnectionStrings:{ConnectionStringName} is not configured, and neither is {DatabaseUrlKey}.");

        services.AddDbContext<LyricBuilderDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<LyricBuilderDbContext>());

        return services;
    }

    /// <summary>
    /// Applies any pending migrations. Containers have no dotnet-ef, so they bring the schema up
    /// to date themselves on startup.
    /// </summary>
    public static async Task MigrateLyricBuilderDatabaseAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LyricBuilderDbContext>();
        await context.Database.MigrateAsync();
    }

    // The named connection string wins; DATABASE_URL is the fallback for hosted deployments.
    private static string? ResolveConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (!string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        var databaseUrl = configuration[DatabaseUrlKey];
        return string.IsNullOrWhiteSpace(databaseUrl) ? null : FromDatabaseUrl(databaseUrl);
    }

    // Npgsql does not read URLs, so postgres://user:password@host:port/database?sslmode=require
    // is translated into its key-value form.
    private static string FromDatabaseUrl(string databaseUrl)
    {
        var uri = new Uri(databaseUrl);
        var credentials = uri.UserInfo.Split(':', 2);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : null
        };

        var query = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in query)
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && parts[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase))
                // verify-full and verify-ca are spelled without the dash in Npgsql's enum.
                builder.SslMode = Enum.Parse<SslMode>(parts[1].Replace("-", ""), ignoreCase: true);
        }

        return builder.ConnectionString;
    }
}
