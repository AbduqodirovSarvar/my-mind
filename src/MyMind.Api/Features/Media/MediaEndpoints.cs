using Microsoft.AspNetCore.Mvc;
using MyMind.Api.Http;
using MyMind.Application.Common;
using MyMind.Application.Features.Media;
using MyMind.Domain.Media;

namespace MyMind.Api.Features.Media;

internal static class MediaEndpoints
{
    public static RouteGroupBuilder MapMediaEndpoints(this RouteGroupBuilder me)
    {
        var media = me.MapGroup("/media").WithTags("Media");

        media.MapPost("/", async (IFormFile? file, MediaService service, CancellationToken ct) =>
            {
                if (file is null)
                {
                    return Error.Validation("Fayl yuborilmadi.", "file").ToProblem();
                }

                await using var content = file.OpenReadStream();
                return (await service.UploadAsync(content, file.Length, file.FileName, ct))
                    .ToHttp(m => TypedResults.Created(m.Url, m));
            })
            // The /api group's filter already checks the X-XSRF-TOKEN header; form binding would demand a second token.
            .DisableAntiforgery()
            .RequireRateLimiting(RateLimitPolicies.Upload)
            .WithMetadata(new RequestSizeLimitAttribute(MediaFile.MaxUploadBytes + 64 * 1024));

        return me;
    }
}
