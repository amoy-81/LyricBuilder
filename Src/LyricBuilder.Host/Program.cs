using System.Text.Json;
using System.Text.Json.Serialization;
using LyricBuilder.Core;
using LyricBuilder.Core.Middlewares;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using Serilog;

const string bearerScheme = "Bearer";

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("Serilog.json", optional: true)
    .AddJsonFile($"Serilog.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

builder.Host.UseSerilog(
    (context, configuration) => configuration.ReadFrom.Configuration(context.Configuration),
    writeToProviders: true);

builder.Services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();

// The single entry point into the application layer — registers services and persistence.
builder.Services.AddCore(builder.Configuration);

builder.Services.AddOpenApi(options =>
{
    // Enums serialize as strings (see JsonStringEnumConverter above); the schema has to agree.
    options.AddSchemaTransformer((schema, context, _) =>
    {
        var type = context.JsonTypeInfo.Type;
        if (type.IsEnum)
        {
            schema.Type = JsonSchemaType.String;
            schema.Format = null;
            schema.Enum = Enum.GetNames(type)
                .Select(name => (System.Text.Json.Nodes.JsonNode)System.Text.Json.Nodes.JsonValue.Create(name))
                .ToList();
        }

        return Task.CompletedTask;
    });

    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "LyricBuilder API";
        document.Info.Version = "1.0.0";
        document.Info.Description = "Lyric authoring platform API";

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[bearerScheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "The accessToken returned by POST /api/auth/login."
        };
        return Task.CompletedTask;
    });

    // Only operations that actually require a caller advertise the scheme, so the docs show
    // which endpoints need a token.
    options.AddOperationTransformer((operation, context, _) =>
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any())
        {
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(bearerScheme, context.Document)] = []
            });
        }

        return Task.CompletedTask;
    });
});

builder.Services.AddMemoryCache();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options
        .WithTitle("LyricBuilder API")
        .WithTheme(ScalarTheme.BluePlanet)
        .WithDefaultHttpClient(ScalarTarget.Shell, ScalarClient.Curl)
        .AddPreferredSecuritySchemes(bearerScheme);
});

app.UseErrorMiddleware();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Reads the authenticated principal into RequestContext — must follow UseAuthentication.
app.UseRequestContext();

app.MapControllers();

app.MapHealthChecks("/live");

app.Run();
