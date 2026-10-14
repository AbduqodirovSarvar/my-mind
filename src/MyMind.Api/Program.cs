using System.Text.Json.Serialization;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using MyMind.Api.Features.Articles;
using MyMind.Api.Features.Auth;
using MyMind.Api.Features.Categories;
using MyMind.Api.Features.Media;
using MyMind.Api.Features.Public;
using MyMind.Api.Http;
using MyMind.Application;
using MyMind.Application.Common;
using MyMind.Infrastructure;
using MyMind.Infrastructure.Storage;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((services, logger) =>
{
    logger.ReadFrom.Configuration(builder.Configuration).ReadFrom.Services(services).Enrich.FromLogContext();

    if (builder.Environment.IsDevelopment())
    {
        logger.WriteTo.Console();
    }
    else
    {
        logger.WriteTo.Console(new RenderedCompactJsonFormatter());
    }
});

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.Configure<PublishingOptions>(builder.Configuration.GetSection(PublishingOptions.Section));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiSecurity(builder.Configuration);
builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Configuration.GetValue<bool>("ForwardedHeaders:Enabled"))
{
    app.UseForwardedHeaders();
}

app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

    // In Docker, Caddy serves /uploads straight from the volume; locally the API does it.
    var storage = app.Services.GetRequiredService<IOptions<StorageOptions>>().Value;
    Directory.CreateDirectory(storage.Root);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(Path.GetFullPath(storage.Root)),
        RequestPath = storage.PublicBasePath,
    });
}

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.MigrateDatabaseAsync();
}

app.MapHealthChecks("/health");

var api = app.MapGroup("/api").AddEndpointFilter<AntiforgeryValidationFilter>();
api.MapAuthEndpoints();
api.MapPublicEndpoints();

var me = api.MapGroup("/me").RequireAuthorization();
me.MapCategoryEndpoints();
me.MapArticleEndpoints();
me.MapMediaEndpoints();

app.Run();

public partial class Program;
