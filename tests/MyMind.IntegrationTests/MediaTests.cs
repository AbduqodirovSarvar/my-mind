using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MyMind.Application.Common;
using MyMind.Application.Features.Media;
using MyMind.Application.Features.Public;
using MyMind.Infrastructure.Persistence;
using MyMind.IntegrationTests.Infrastructure;
using NetVips;

namespace MyMind.IntegrationTests;

/// <summary>Phase 3 gate (backend part): an article with an uploaded image is written and published.</summary>
public class MediaTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Upload_StoresAWebpCopy_ThatAPublishedArticleCanShow()
    {
        var user = await TestUser.RegisterAsync(factory);

        var upload = await UploadAsync(user, Jpeg(2400, 1600), "rasm.jpg");

        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var media = (await upload.Content.ReadFromJsonAsync<MediaResponse>(TestUser.Json, Ct))!;
        Assert.Matches("^/uploads/[a-f0-9]{32}/\\d{4}/[a-f0-9]{32}\\.webp$", media.Url);
        Assert.Equal((1920, 1280), (media.Width, media.Height));
        Assert.True(File.Exists(factory.StoragePath(media.Url)));
        Assert.True(File.Exists(factory.StoragePath(media.SmallUrl!)));

        var category = await user.CreateCategoryAsync("Rasmlar");
        var document = ImageDocument(media.Url, media.Width, media.Height);
        await user.PublishAsync(category.Id, "Rasmli maqola", document);

        var article = await factory.CreateClient().GetFromJsonAsync<PublicArticleResponse>(
            $"/api/users/{user.UserName}/articles/rasmli-maqola", TestUser.Json, Ct);
        Assert.Contains($"src=\"{media.Url}\"", article!.Html);
        Assert.Contains($"{media.SmallUrl} 640w", article.Html);
    }

    [Theory]
    [InlineData("svg")]
    [InlineData("html")]
    [InlineData("corrupt-jpeg")]
    public async Task Upload_RejectsFilesThatAreNotImages(string kind)
    {
        var user = await TestUser.RegisterAsync(factory);
        byte[] content = kind switch
        {
            "svg" => "<svg xmlns=\"http://www.w3.org/2000/svg\" onload=\"alert(1)\"/>"u8.ToArray(),
            "html" => "<html><script>alert(1)</script></html>"u8.ToArray(),
            _ => [0xFF, 0xD8, 0xFF, 0xE0, 0, 1, 2, 3, 4, 5],
        };

        var upload = await UploadAsync(user, content, "rasm.png");

        Assert.Equal(HttpStatusCode.BadRequest, upload.StatusCode);
    }

    [Fact]
    public async Task Upload_RejectsFilesOverTheLimit()
    {
        var user = await TestUser.RegisterAsync(factory);
        var tooBig = new byte[(5 * 1024 * 1024) + 1];
        tooBig[0] = 0xFF;
        tooBig[1] = 0xD8;
        tooBig[2] = 0xFF;

        var upload = await UploadAsync(user, tooBig, "katta.jpg");

        Assert.Equal(HttpStatusCode.BadRequest, upload.StatusCode);
    }

    [Fact]
    public async Task Upload_RequiresSignInAndXsrfToken()
    {
        var anonymous = factory.CreateBrowserClient();
        await anonymous.GetAsync("/api/auth/me", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, (await UploadAsync(anonymous, Jpeg(10, 10), "a.jpg")).StatusCode);

        // Signed in, but the upload carries no X-XSRF-TOKEN header.
        var client = ApiFactory.CreateBrowserClient(factory, sendXsrfHeader: false);
        var me = await client.GetAsync("/api/auth/me", Ct);
        var token = me.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));
        using var register = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register")
        {
            Content = JsonContent.Create(new Application.Features.Auth.RegisterRequest(
                $"x{Guid.NewGuid():N}@example.com", $"x_{Guid.NewGuid():N}"[..20], "correct-horse-battery", null, true)),
            Headers = { { "X-XSRF-TOKEN", token["XSRF-TOKEN=".Length..token.IndexOf(';')] } },
        };
        Assert.Equal(HttpStatusCode.Created, (await client.SendAsync(register, Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await UploadAsync(client, Jpeg(10, 10), "a.jpg")).StatusCode);
    }

    [Fact]
    public async Task Cleanup_RemovesUnusedUploadsAndOldDeletedArticles_AndKeepsImagesInDrafts()
    {
        var user = await TestUser.RegisterAsync(factory);
        var unused = (await (await UploadAsync(user, Jpeg(100, 100), "a.jpg")).Content.ReadFromJsonAsync<MediaResponse>(TestUser.Json, Ct))!;
        var used = (await (await UploadAsync(user, Jpeg(100, 100), "b.jpg")).Content.ReadFromJsonAsync<MediaResponse>(TestUser.Json, Ct))!;

        var category = await user.CreateCategoryAsync("Tozalash");
        var (kept, keptETag) = await user.CreateArticleAsync(category.Id, "Saqlanadi");
        (await user.SaveDraftAsync(kept.Id, "Saqlanadi", ImageDocument(used.Url), keptETag))
            .EnsureSuccessStatusCode();
        var (deleted, _) = await user.CreateArticleAsync(category.Id, "O'chiriladi");
        await user.Client.DeleteAsync($"/api/me/articles/{deleted.Id}", Ct);

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AppDbContext>();
        var cleanup = new ContentCleanupService(
            db, services.GetRequiredService<IFileStorage>(), new ShiftedClock(TimeSpan.FromDays(31)), NullLogger<ContentCleanupService>.Instance);

        await cleanup.RunAsync(Ct);

        Assert.False(await db.Media.AnyAsync(m => m.Id == unused.Id, Ct));
        Assert.False(File.Exists(factory.StoragePath(unused.Url)));
        Assert.True(await db.Media.AnyAsync(m => m.Id == used.Id, Ct));
        Assert.True(File.Exists(factory.StoragePath(used.Url)));
        Assert.False(await db.Articles.IgnoreQueryFilters().AnyAsync(a => a.Id == deleted.Id, Ct));
        Assert.True(await db.Articles.AnyAsync(a => a.Id == kept.Id, Ct));
    }

    private static string ImageDocument(string src, int? width = null, int? height = null) =>
        JsonSerializer.Serialize(new { type = "doc", content = new[] { new { type = "image", attrs = new { src, alt = "Sxema", width, height } } } });

    private static byte[] Jpeg(int width, int height)
    {
        using var image = Image.Black(width, height, bands: 3) + 90;
        return image.JpegsaveBuffer();
    }

    private static Task<HttpResponseMessage> UploadAsync(TestUser user, byte[] content, string fileName) =>
        UploadAsync(user.Client, content, fileName);

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, byte[] content, string fileName)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "file", fileName);
        return await client.PostAsync("/api/me/media", form, Ct);
    }

    /// <summary>"Now" moved into the future, so grace periods have passed.</summary>
    private sealed class ShiftedClock(TimeSpan shift) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + shift;
    }
}
