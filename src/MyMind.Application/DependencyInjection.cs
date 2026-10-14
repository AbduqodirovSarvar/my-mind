using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using MyMind.Application.Features.Articles;
using MyMind.Application.Features.Categories;
using MyMind.Application.Features.Media;
using MyMind.Application.Features.Public;

namespace MyMind.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddScoped<CategoryService>();
        services.AddScoped<ArticleService>();
        services.AddScoped<PublicReadService>();
        services.AddScoped<MediaService>();
        services.AddScoped<ContentCleanupService>();

        return services;
    }
}
