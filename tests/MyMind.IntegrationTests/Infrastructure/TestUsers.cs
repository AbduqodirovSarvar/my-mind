using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MyMind.Application.Features.Articles;
using MyMind.Application.Features.Auth;
using MyMind.Application.Features.Categories;

namespace MyMind.IntegrationTests.Infrastructure;

/// <summary>A signed-in browser-like client and the requests most tests need.</summary>
public sealed record TestUser(HttpClient Client, string UserName)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<TestUser> RegisterAsync(ApiFactory factory)
    {
        var client = factory.CreateBrowserClient();
        await client.GetAsync("/api/auth/me", Ct);

        var id = Guid.NewGuid().ToString("N")[..12];
        var userName = $"u_{id}";
        var response = await client.PostAsJsonAsync(
            "/api/auth/register", new RegisterRequest($"{userName}@example.com", userName, "correct-horse-battery", null, true), Ct);
        response.EnsureSuccessStatusCode();

        return new TestUser(client, userName);
    }

    public async Task<CategoryResponse> CreateCategoryAsync(string name, Guid? parentId = null)
    {
        var response = await Client.PostAsJsonAsync("/api/me/categories", new CreateCategoryRequest(name, parentId), Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CategoryResponse>(Json, Ct))!;
    }

    public async Task<(ArticleDetailResponse Article, EntityTagHeaderValue ETag)> CreateArticleAsync(Guid categoryId, string title)
    {
        var response = await Client.PostAsJsonAsync("/api/me/articles", new CreateArticleRequest(categoryId, title), Ct);
        response.EnsureSuccessStatusCode();
        return ((await response.Content.ReadFromJsonAsync<ArticleDetailResponse>(Json, Ct))!, response.Headers.ETag!);
    }

    public async Task<HttpResponseMessage> SaveDraftAsync(Guid articleId, string title, string documentJson, EntityTagHeaderValue? etag)
    {
        using var content = JsonDocument.Parse(documentJson);
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/me/articles/{articleId}/draft")
        {
            Content = JsonContent.Create(new SaveDraftRequest(title, content.RootElement.Clone()), options: Json),
        };
        if (etag is not null)
        {
            request.Headers.IfMatch.Add(etag);
        }

        return await Client.SendAsync(request, Ct);
    }

    /// <summary>Creates an article with the given content and publishes it.</summary>
    public async Task<ArticleDetailResponse> PublishAsync(Guid categoryId, string title, string documentJson)
    {
        var (article, etag) = await CreateArticleAsync(categoryId, title);
        var saved = await SaveDraftAsync(article.Id, title, documentJson, etag);
        saved.EnsureSuccessStatusCode();

        var published = await Client.PostAsync($"/api/me/articles/{article.Id}/publish", null, Ct);
        published.EnsureSuccessStatusCode();
        return (await published.Content.ReadFromJsonAsync<ArticleDetailResponse>(Json, Ct))!;
    }
}
