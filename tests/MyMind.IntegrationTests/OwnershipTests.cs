using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MyMind.Application.Features.Articles;
using MyMind.Application.Features.Categories;
using MyMind.IntegrationTests.Infrastructure;

namespace MyMind.IntegrationTests;

/// <summary>
/// Phase 2 gate (plan §13.1, IDOR): another user's ids behave exactly like ids that do not exist —
/// every /api/me endpoint answers 404 and nothing changes.
/// </summary>
public class OwnershipTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EveryMeEndpoint_TreatsAnotherUsersResourcesAsMissing()
    {
        var owner = await TestUser.RegisterAsync(factory);
        var category = await owner.CreateCategoryAsync("Shaxsiy");
        var article = await owner.PublishAsync(category.Id, "Maxfiy", """{"type":"doc","content":[]}""");
        var revisions = await owner.Client.GetFromJsonAsync<List<RevisionResponse>>($"/api/me/articles/{article.Id}/revisions", TestUser.Json, Ct);
        var revisionId = revisions![0].Id;
        var ownerETag = (await owner.Client.GetAsync($"/api/me/articles/{article.Id}", Ct)).Headers.ETag!;

        var intruder = await TestUser.RegisterAsync(factory);
        var intruderCategory = await intruder.CreateCategoryAsync("Meniki");

        var attempts = new (string Name, Func<Task<HttpResponseMessage>> Send)[]
        {
            ("rename category", () => intruder.Client.PutAsJsonAsync($"/api/me/categories/{category.Id}", new UpdateCategoryRequest("x", null), Ct)),
            ("delete category", () => intruder.Client.DeleteAsync($"/api/me/categories/{category.Id}", Ct)),
            ("create subcategory under it", () => intruder.Client.PostAsJsonAsync("/api/me/categories", new CreateCategoryRequest("x", category.Id), Ct)),
            ("reorder it", () => intruder.Client.PutAsJsonAsync("/api/me/categories/order", new ReorderCategoriesRequest([new CategoryPosition(category.Id, null, 0)]), Ct)),
            ("move own category under it", () => intruder.Client.PutAsJsonAsync("/api/me/categories/order", new ReorderCategoriesRequest([new CategoryPosition(intruderCategory.Id, category.Id, 0)]), Ct)),
            ("create article in it", () => intruder.Client.PostAsJsonAsync("/api/me/articles", new CreateArticleRequest(category.Id, "x"), Ct)),
            ("read article", () => intruder.Client.GetAsync($"/api/me/articles/{article.Id}", Ct)),
            ("save draft", () => SaveDraftWithETag(intruder, article.Id, ownerETag)),
            ("change meta", () => intruder.Client.PutAsJsonAsync($"/api/me/articles/{article.Id}/meta", new UpdateArticleMetaRequest(intruderCategory.Id, "x", null), Ct)),
            ("publish", () => intruder.Client.PostAsync($"/api/me/articles/{article.Id}/publish", null, Ct)),
            ("unpublish", () => intruder.Client.PostAsync($"/api/me/articles/{article.Id}/unpublish", null, Ct)),
            ("delete article", () => intruder.Client.DeleteAsync($"/api/me/articles/{article.Id}", Ct)),
            ("list revisions", () => intruder.Client.GetAsync($"/api/me/articles/{article.Id}/revisions", Ct)),
            ("restore revision", () => intruder.Client.PostAsync($"/api/me/articles/{article.Id}/revisions/{revisionId}/restore", null, Ct)),
        };

        foreach (var (name, send) in attempts)
        {
            var response = await send();
            Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"{name}: expected 404, got {(int)response.StatusCode}");
        }

        // The intruder's own list does not show the owner's article, and nothing of the owner's changed.
        var intruderArticles = await intruder.Client.GetFromJsonAsync<Application.Common.PagedResponse<ArticleListItem>>("/api/me/articles", TestUser.Json, Ct);
        Assert.Empty(intruderArticles!.Items);

        var ownerTree = await owner.Client.GetFromJsonAsync<List<CategoryResponse>>("/api/me/categories", TestUser.Json, Ct);
        Assert.Equal("Shaxsiy", Assert.Single(ownerTree!).Name);
        var ownerArticle = await owner.Client.GetFromJsonAsync<ArticleDetailResponse>($"/api/me/articles/{article.Id}", TestUser.Json, Ct);
        Assert.Equal("Maxfiy", ownerArticle!.DraftTitle);
        Assert.Equal(Domain.Articles.ArticleStatus.Published, ownerArticle.Status);
    }

    [Fact]
    public async Task MeEndpoints_RequireSignIn()
    {
        var anonymous = factory.CreateBrowserClient();
        await anonymous.GetAsync("/api/auth/me", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/me/categories", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/me/articles", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/me/categories", new CreateCategoryRequest("x", null), Ct)).StatusCode);
    }

    private static Task<HttpResponseMessage> SaveDraftWithETag(TestUser user, Guid articleId, EntityTagHeaderValue etag) =>
        user.SaveDraftAsync(articleId, "buzildi", """{"type":"doc","content":[]}""", etag);
}
