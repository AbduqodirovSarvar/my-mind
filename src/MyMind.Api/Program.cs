using System.Text.Json.Serialization;
using MyMind.Api.Features.Articles;
using MyMind.Api.Features.Auth;
using MyMind.Api.Features.Categories;
using MyMind.Api.Features.Public;
using MyMind.Api.Http;
using MyMind.Application;
using MyMind.Application.Common;
using MyMind.Infrastructure;
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

app.Run();

public partial class Program;
