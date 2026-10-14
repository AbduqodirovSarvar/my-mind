using MyMind.Api.Http;
using MyMind.Application.Features.Categories;

namespace MyMind.Api.Features.Categories;

internal static class CategoryEndpoints
{
    public static RouteGroupBuilder MapCategoryEndpoints(this RouteGroupBuilder me)
    {
        var categories = me.MapGroup("/categories").WithTags("Categories");

        categories.MapGet("/", async (CategoryService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetTreeAsync(ct)));

        categories.MapPost("/", async (CreateCategoryRequest request, CategoryService service, CancellationToken ct) =>
                (await service.CreateAsync(request, ct)).ToHttp(c => TypedResults.Created($"/api/me/categories/{c.Id}", c)))
            .WithValidation<CreateCategoryRequest>();

        categories.MapPut("/{id:guid}", async (Guid id, UpdateCategoryRequest request, CategoryService service, CancellationToken ct) =>
                (await service.UpdateAsync(id, request, ct)).ToHttp(TypedResults.Ok))
            .WithValidation<UpdateCategoryRequest>();

        categories.MapDelete("/{id:guid}", async (Guid id, CategoryService service, CancellationToken ct) =>
            (await service.DeleteAsync(id, ct)).ToHttp(_ => TypedResults.NoContent()));

        // Drag-and-drop: new parents and positions for any number of categories at once.
        categories.MapPut("/order", async (ReorderCategoriesRequest request, CategoryService service, CancellationToken ct) =>
                (await service.ReorderAsync(request, ct)).ToHttp(TypedResults.Ok))
            .WithValidation<ReorderCategoriesRequest>();

        return me;
    }
}
