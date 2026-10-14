using System.Net;
using System.Net.Http.Json;
using MyMind.Application.Features.Categories;
using MyMind.IntegrationTests.Infrastructure;

namespace MyMind.IntegrationTests;

public class CategoryTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_BuildsATwoLevelTree_WithUniqueSlugs()
    {
        var user = await TestUser.RegisterAsync(factory);

        var backend = await user.CreateCategoryAsync("Backend");
        var backendAgain = await user.CreateCategoryAsync("backend!");
        var dotnet = await user.CreateCategoryAsync(".NET", backend.Id);

        Assert.Equal("backend", backend.Slug);
        Assert.Equal("backend-2", backendAgain.Slug);
        Assert.Equal("net", dotnet.Slug);

        var tree = await user.Client.GetFromJsonAsync<List<CategoryResponse>>("/api/me/categories", TestUser.Json, Ct);
        Assert.Equal(["backend", "backend-2"], tree!.Select(c => c.Slug));
        Assert.Equal(dotnet.Id, Assert.Single(tree![0].Children).Id);
    }

    [Fact]
    public async Task Create_RejectsAThirdLevel()
    {
        var user = await TestUser.RegisterAsync(factory);
        var root = await user.CreateCategoryAsync("Root");
        var child = await user.CreateCategoryAsync("Child", root.Id);

        var response = await user.Client.PostAsJsonAsync("/api/me/categories", new CreateCategoryRequest("Grandchild", child.Id), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_FollowsTheName_OrTakesAnExplicitSlug()
    {
        var user = await TestUser.RegisterAsync(factory);
        var category = await user.CreateCategoryAsync("Eski nom");
        await user.CreateCategoryAsync("Band");

        var renamed = await user.Client.PutAsJsonAsync($"/api/me/categories/{category.Id}", new UpdateCategoryRequest("Yangi nom", null), Ct);
        Assert.Equal("yangi-nom", (await renamed.Content.ReadFromJsonAsync<CategoryResponse>(TestUser.Json, Ct))!.Slug);

        var taken = await user.Client.PutAsJsonAsync($"/api/me/categories/{category.Id}", new UpdateCategoryRequest("X", "band"), Ct);
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
    }

    [Fact]
    public async Task Delete_IsRefused_WhileTheCategoryHasChildrenOrArticles()
    {
        var user = await TestUser.RegisterAsync(factory);
        var root = await user.CreateCategoryAsync("Root");
        var child = await user.CreateCategoryAsync("Child", root.Id);
        var (article, _) = await user.CreateArticleAsync(child.Id, "Maqola");

        Assert.Equal(HttpStatusCode.Conflict, (await user.Client.DeleteAsync($"/api/me/categories/{root.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await user.Client.DeleteAsync($"/api/me/categories/{child.Id}", Ct)).StatusCode);

        // A soft-deleted article no longer blocks the category.
        await user.Client.DeleteAsync($"/api/me/articles/{article.Id}", Ct);
        Assert.Equal(HttpStatusCode.NoContent, (await user.Client.DeleteAsync($"/api/me/categories/{child.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await user.Client.DeleteAsync($"/api/me/categories/{root.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Reorder_MovesCategories_AndKeepsTheTwoLevelRule()
    {
        var user = await TestUser.RegisterAsync(factory);
        var a = await user.CreateCategoryAsync("A");
        var b = await user.CreateCategoryAsync("B");
        var c = await user.CreateCategoryAsync("C", a.Id);

        // B goes under A, before C.
        var moved = await user.Client.PutAsJsonAsync("/api/me/categories/order", new ReorderCategoriesRequest(
            [new CategoryPosition(b.Id, a.Id, 0), new CategoryPosition(c.Id, a.Id, 1)]), Ct);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        var tree = (await moved.Content.ReadFromJsonAsync<List<CategoryResponse>>(TestUser.Json, Ct))!;
        Assert.Equal([b.Id, c.Id], Assert.Single(tree).Children.Select(x => x.Id));

        // A (which has children) cannot go under B.
        var tooDeep = await user.Client.PutAsJsonAsync("/api/me/categories/order", new ReorderCategoriesRequest(
            [new CategoryPosition(a.Id, b.Id, 0)]), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, tooDeep.StatusCode);
    }
}
