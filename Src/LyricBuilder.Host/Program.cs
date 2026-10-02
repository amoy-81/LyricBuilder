using System.Text.Json;
using System.Text.Json.Serialization;
using LyricBuilder.Core;
using LyricBuilder.Core.Middlewares;
using LyricBuilder.Infrastructure;
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

// Enums travel as their names, both ways. Numbers are refused, so a request cannot depend on
// the order of an enum's members.
var enumsAsNames = new JsonStringEnumConverter(allowIntegerValues: false);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.Converters.Add(enumsAsNames);
    });

// The OpenAPI document is generated from these options, not the MVC ones above. Without the
// converter here too, it describes every enum as an integer.
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(enumsAsNames));

builder.Services.AddEndpointsApiExplorer();

// The single entry point into the application layer — registers services and persistence.
builder.Services.AddCore(builder.Configuration);

builder.Services.AddOpenApi(options =>
{
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

// Browser clients allowed to call the API. Tokens travel in the Authorization header, not
// cookies, so credentials are not allowed.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddMemoryCache();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Off by default, so local runs keep using dotnet-ef. The Docker image turns it on.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
    await app.Services.MigrateLyricBuilderDatabaseAsync();

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

// Before the HTTPS redirect, so a preflight is answered instead of redirected.
app.UseCors();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Reads the authenticated principal into RequestContext — must follow UseAuthentication.
app.UseRequestContext();

app.MapControllers();

app.MapHealthChecks("/live");

app.Run();
