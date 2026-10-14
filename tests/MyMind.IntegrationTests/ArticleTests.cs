using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using MyMind.Application.Features.Articles;
using MyMind.Application.Features.Public;
using MyMind.Domain.Articles;
using MyMind.IntegrationTests.Infrastructure;

namespace MyMind.IntegrationTests;

public class ArticleTests(ApiFactory factory)
{
    private const string Body = """
        {"type":"doc","content":[
          {"type":"heading","attrs":{"level":2},"content":[{"type":"text","text":"Kirish"}]},
          {"type":"paragraph","content":[{"type":"text","text":"Dependency injection haqida."}]}]}
        """;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Draft_RequiresTheCurrentVersion()
    {
        var user = await TestUser.RegisterAsync(factory);
        var category = await user.CreateCategoryAsync("Backend");
        var (article, etag) = await user.CreateArticleAsync(category.Id, "DI nima");

        var withoutIfMatch = await user.SaveDraftAsync(article.Id, "DI nima", Body, etag: null);
        Assert.Equal(HttpStatusCode.PreconditionRequired, withoutIfMatch.StatusCode);

        var first = await user.SaveDraftAsync(article.Id, "DI nima", Body, etag);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.NotEqual(etag, first.Headers.ETag);

        // A second tab still holding the old ETag must not overwrite the first one's work.
        var stale = await user.SaveDraftAsync(article.Id, "Eski oyna", Body, etag);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);

        var next = await user.SaveDraftAsync(article.Id, "DI nima?", Body, first.Headers.ETag);
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
    }

    [Fact]
    public async Task Publish_MakesTheArticlePublic_AndDraftChangesStayPrivateUntilRepublished()
    {
        var user = await TestUser.RegisterAsync(factory);
        var backend = await user.CreateCategoryAsync("Backend");
        var dotnet = await user.CreateCategoryAsync(".NET", backend.Id);
        var published = await user.PublishAsync(dotnet.Id, "DI nima", Body);
        var anonymous = factory.CreateClient();

        var article = await anonymous.GetFromJsonAsync<PublicArticleResponse>($"/api/users/{user.UserName}/articles/di-nima", TestUser.Json, Ct);
        Assert.Equal("DI nima", article!.Title);
        Assert.Equal("<h2 id=\"kirish\">Kirish</h2><p>Dependency injection haqida.</p>", article.Html);
        Assert.Equal(["backend", "net"], article.CategoryPath.Select(c => c.Slug));
        Assert.Equal("kirish", article.Toc[0].GetProperty("id").GetString());

        var tree = await anonymous.GetFromJsonAsync<TreeResponse>($"/api/users/{user.UserName.ToUpperInvariant()}/tree", TestUser.Json, Ct);
        Assert.Equal("di-nima", Assert.Single(Assert.Single(Assert.Single(tree!.Categories).Children).Articles).Slug);

        var detail = await user.Client.GetAsync($"/api/me/articles/{published.Id}", Ct);
        await user.SaveDraftAsync(published.Id, "Yangi sarlavha", Body, detail.Headers.ETag);
        var stillOld = await anonymous.GetFromJsonAsync<PublicArticleResponse>($"/api/users/{user.UserName}/articles/di-nima", TestUser.Json, Ct);
        Assert.Equal("DI nima", stillOld!.Title);

        var mine = await user.Client.GetFromJsonAsync<ArticleDetailResponse>($"/api/me/articles/{published.Id}", TestUser.Json, Ct);
        Assert.True(mine!.HasUnpublishedChanges);
    }

    [Fact]
    public async Task Unpublish_AndDelete_HideTheArticle()
    {
        var user = await TestUser.RegisterAsync(factory);
        var category = await user.CreateCategoryAsync("Notes");
        var article = await user.PublishAsync(category.Id, "Yashirin", Body);
        var anonymous = factory.CreateClient();
        var url = $"/api/users/{user.UserName}/articles/yashirin";

        await user.Client.PostAsync($"/api/me/articles/{article.Id}/unpublish", null, Ct);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(url, Ct)).StatusCode);

        await user.Client.DeleteAsync($"/api/me/articles/{article.Id}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.GetAsync($"/api/me/articles/{article.Id}", Ct)).StatusCode);

        // The deleted article's slug is free again.
        var (again, _) = await user.CreateArticleAsync(category.Id, "Yashirin");
        Assert.Equal("yashirin", again.Slug);
    }

    [Fact]
    public async Task ChangingThePublishedSlug_RedirectsTheOldUrl()
    {
        var user = await TestUser.RegisterAsync(factory);
        var category = await user.CreateCategoryAsync("Notes");
        var article = await user.PublishAsync(category.Id, "Eski nom", Body);

        var meta = await user.Client.PutAsJsonAsync(
            $"/api/me/articles/{article.Id}/meta", new UpdateArticleMetaRequest(category.Id, "yangi-nom", "Qisqacha."), Ct);
        Assert.Equal(HttpStatusCode.OK, meta.StatusCode);

        var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var old = await anonymous.GetAsync($"/api/users/{user.UserName}/articles/eski-nom", Ct);
        Assert.Equal(HttpStatusCode.MovedPermanently, old.StatusCode);
        Assert.Equal($"/api/users/{user.UserName}/articles/yangi-nom", old.Headers.Location!.OriginalString);

        var current = await anonymous.GetFromJsonAsync<PublicArticleResponse>($"/api/users/{user.UserName}/articles/yangi-nom", TestUser.Json, Ct);
        Assert.Equal("Qisqacha.", current!.Excerpt);
    }

    [Fact]
    public async Task Publish_RejectsUnknownContent_AndKeepsTheArticleUnpublished()
    {
        var user = await TestUser.RegisterAsync(factory);
        var category = await user.CreateCategoryAsync("Notes");
        var (article, etag) = await user.CreateArticleAsync(category.Id, "Buzuq");
        await user.SaveDraftAsync(article.Id, "Buzuq", """{"type":"doc","content":[{"type":"iframe","attrs":{"src":"https://evil.example"}}]}""", etag);

        var publish = await user.Client.PostAsync($"/api/me/articles/{article.Id}/publish", null, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, publish.StatusCode);
        var mine = await user.Client.GetFromJsonAsync<ArticleDetailResponse>($"/api/me/articles/{article.Id}", TestUser.Json, Ct);
        Assert.Equal(ArticleStatus.Draft, mine!.Status);
    }

    [Fact]
    public async Task Publish_StoresOnlySanitizedHtml()
    {
        var user = await TestUser.RegisterAsync(factory);
        var category = await user.CreateCategoryAsync("Notes");
        const string payload = """
            {"type":"doc","content":[{"type":"paragraph","content":[
              {"type":"text","text":"<script>alert(1)</script>"},
              {"type":"text","text":"bos","marks":[{"type":"link","attrs":{"href":"javascript:alert(1)"}}]}]}]}
            """;
        await user.PublishAsync(category.Id, "XSS", payload);

        var article = await factory.CreateClient().GetFromJsonAsync<PublicArticleResponse>($"/api/users/{user.UserName}/articles/xss", TestUser.Json, Ct);

        Assert.Equal("<p>&lt;script&gt;alert(1)&lt;/script&gt;bos</p>", article!.Html);
    }

    [Fact]
    public async Task Revisions_KeepEveryPublish_AndCanBeRestoredIntoTheDraft()
    {
        var user = await TestUser.RegisterAsync(factory);
        var category = await user.CreateCategoryAsync("Notes");
        var article = await user.PublishAsync(category.Id, "Birinchi", Body);

        var detail = await user.Client.GetAsync($"/api/me/articles/{article.Id}", Ct);
        await user.SaveDraftAsync(article.Id, "Ikkinchi", """{"type":"doc","content":[]}""", detail.Headers.ETag);

        var revisions = await user.Client.GetFromJsonAsync<List<RevisionResponse>>($"/api/me/articles/{article.Id}/revisions", TestUser.Json, Ct);
        var published = Assert.Single(revisions!, r => r.Kind == RevisionKind.Publish);
        Assert.Equal("Birinchi", published.Title);

        var restored = await user.Client.PostAsync($"/api/me/articles/{article.Id}/revisions/{published.Id}/restore", null, Ct);
        var draft = (await restored.Content.ReadFromJsonAsync<ArticleDetailResponse>(TestUser.Json, Ct))!;
        Assert.Equal("Birinchi", draft.DraftTitle);
        Assert.Equal(JsonValueKind.Array, draft.DraftContent.GetProperty("content").ValueKind);
        Assert.Equal(2, draft.DraftContent.GetProperty("content").GetArrayLength());
    }

    [Fact]
    public async Task PublicEndpoints_ReturnNotFound_ForUnknownAuthorsAndDrafts()
    {
        var user = await TestUser.RegisterAsync(factory);
        var category = await user.CreateCategoryAsync("Notes");
        await user.CreateArticleAsync(category.Id, "Qoralama");
        var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/api/users/no_such_user_x/tree", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/users/{user.UserName}/articles/qoralama", Ct)).StatusCode);

        // Categories without published articles are not part of the public tree.
        var tree = await anonymous.GetFromJsonAsync<TreeResponse>($"/api/users/{user.UserName}/tree", TestUser.Json, Ct);
        Assert.Empty(tree!.Categories);
    }
}
