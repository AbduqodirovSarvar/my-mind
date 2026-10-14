using MyMind.Api.Http;
using MyMind.Application.Common;
using MyMind.Application.Features.Public;

namespace MyMind.Api.Features.Public;

internal static class PublicEndpoints
{
    public static RouteGroupBuilder MapPublicEndpoints(this RouteGroupBuilder api)
    {
        var users = api.MapGroup("/users/{userName}").WithTags("Public");

        users.MapGet("/", async (string userName, PublicReadService service, CancellationToken ct) =>
            await service.GetUserAsync(userName, ct) is { } user
                ? TypedResults.Ok(user)
                : AuthorNotFound());

        users.MapGet("/tree", async (string userName, PublicReadService service, CancellationToken ct) =>
            await service.GetTreeAsync(userName, ct) is { } tree
                ? TypedResults.Ok(tree)
                : AuthorNotFound());

        // A renamed article answers its old slug with a permanent redirect (plan §5.2).
        users.MapGet("/articles/{slug}", async (string userName, string slug, PublicReadService service, CancellationToken ct) =>
            await service.GetArticleAsync(userName, slug, ct) switch
            {
                { Article: { } article } => TypedResults.Ok(article),
                { MovedToSlug: { } movedTo } => TypedResults.Redirect(
                    $"/api/users/{Uri.EscapeDataString(userName)}/articles/{movedTo}", permanent: true),
                _ => Error.NotFound("Maqola topilmadi.").ToProblem(),
            });

        return api;
    }

    private static IResult AuthorNotFound() => Error.NotFound("Muallif topilmadi.").ToProblem();
}
