using Microsoft.AspNetCore.Mvc;
using MyMind.Api.Http;
using MyMind.Application.Common;
using MyMind.Application.Features.Articles;
using MyMind.Domain.Articles;

namespace MyMind.Api.Features.Articles;

internal static class ArticleEndpoints
{
    public static RouteGroupBuilder MapArticleEndpoints(this RouteGroupBuilder me)
    {
        var articles = me.MapGroup("/articles").WithTags("Articles");

        articles.MapGet("/", async (ArticleService service, CancellationToken ct, ArticleStatus? status, int page = 1, int pageSize = 20) =>
            TypedResults.Ok(await service.ListAsync(status, page, pageSize, ct)));

        articles.MapPost("/", async (CreateArticleRequest request, ArticleService service, HttpContext http, CancellationToken ct) =>
                (await service.CreateAsync(request, ct)).ToHttp(a => WithETag(http, a, TypedResults.Created($"/api/me/articles/{a.Id}", a))))
            .WithValidation<CreateArticleRequest>();

        articles.MapGet("/{id:guid}", async (Guid id, ArticleService service, HttpContext http, CancellationToken ct) =>
            (await service.GetAsync(id, ct)).ToHttp(a => WithETag(http, a)));

        articles.MapPut("/{id:guid}/draft", async (Guid id, SaveDraftRequest request, ArticleService service, HttpContext http, CancellationToken ct) =>
                (await service.SaveDraftAsync(id, request, ETags.ParseIfMatch(http.Request), ct)).ToHttp(a => WithETag(http, a)))
            .WithValidation<SaveDraftRequest>()
            .WithMetadata(new RequestSizeLimitAttribute(SaveDraftRequestValidator.MaxContentBytes + 64 * 1024));

        articles.MapPut("/{id:guid}/meta", async (Guid id, UpdateArticleMetaRequest request, ArticleService service, HttpContext http, CancellationToken ct) =>
                (await service.UpdateMetaAsync(id, request, ETags.ParseIfMatch(http.Request), ct)).ToHttp(a => WithETag(http, a)))
            .WithValidation<UpdateArticleMetaRequest>();

        articles.MapPost("/{id:guid}/publish", async (Guid id, ArticleService service, HttpContext http, CancellationToken ct) =>
            (await service.PublishAsync(id, ETags.ParseIfMatch(http.Request), ct)).ToHttp(a => WithETag(http, a)));

        articles.MapPost("/{id:guid}/unpublish", async (Guid id, ArticleService service, HttpContext http, CancellationToken ct) =>
            (await service.UnpublishAsync(id, ct)).ToHttp(a => WithETag(http, a)));

        articles.MapDelete("/{id:guid}", async (Guid id, ArticleService service, CancellationToken ct) =>
            (await service.DeleteAsync(id, ct)).ToHttp(_ => TypedResults.NoContent()));

        articles.MapGet("/{id:guid}/revisions", async (Guid id, ArticleService service, CancellationToken ct) =>
            (await service.ListRevisionsAsync(id, ct)).ToHttp(TypedResults.Ok));

        articles.MapPost("/{id:guid}/revisions/{revisionId:guid}/restore", async (Guid id, Guid revisionId, ArticleService service, HttpContext http, CancellationToken ct) =>
            (await service.RestoreRevisionAsync(id, revisionId, ct)).ToHttp(a => WithETag(http, a)));

        return me;
    }

    /// <summary>Every response that carries an article also carries its current version.</summary>
    private static IResult WithETag(HttpContext http, ArticleDetailResponse article, IResult? result = null)
    {
        http.Response.Headers.ETag = ETags.Format(article.Version);
        return result ?? TypedResults.Ok(article);
    }
}
