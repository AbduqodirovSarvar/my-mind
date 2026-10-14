using System.Globalization;
using Microsoft.Net.Http.Headers;
using MyMind.Application.Common;

namespace MyMind.Api.Http;

internal static class ResultHttpExtensions
{
    public static IResult ToHttp<T>(this Result<T> result, Func<T, IResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value) : result.Error!.ToProblem();

    public static IResult ToProblem(this Error error)
    {
        var status = error.Kind switch
        {
            ErrorKind.Validation => StatusCodes.Status400BadRequest,
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            ErrorKind.PreconditionFailed => StatusCodes.Status412PreconditionFailed,
            ErrorKind.PreconditionRequired => StatusCodes.Status428PreconditionRequired,
            ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError,
        };

        // Field errors use the same "errors" shape as validation problems, so the UI shows them by the field.
        var extensions = error.Field is null
            ? null
            : new Dictionary<string, object?> { ["errors"] = new Dictionary<string, string[]> { [error.Field] = [error.Message] } };

        return TypedResults.Problem(statusCode: status, title: error.Message, extensions: extensions);
    }
}

/// <summary>Article versions travel as strong ETags: <c>"123"</c>.</summary>
internal static class ETags
{
    public static string Format(uint version) => $"\"{version.ToString(CultureInfo.InvariantCulture)}\"";

    /// <summary>
    /// Reads the version from If-Match. Compressing proxies rewrite ETags they pass through —
    /// Caddy appends the encoding (<c>"835-zstd"</c>), nginx weakens them (<c>W/"835"</c>) — and
    /// clients echo what they received, so both forms are accepted.
    /// </summary>
    public static uint? ParseIfMatch(HttpRequest request) => ParseIfMatch(request.Headers[HeaderNames.IfMatch].ToString());

    public static uint? ParseIfMatch(string header)
    {
        var value = header.Trim();
        if (value.StartsWith("W/", StringComparison.Ordinal))
        {
            value = value[2..];
        }

        if (value.Length < 3 || value[0] != '"' || value[^1] != '"')
        {
            return null;
        }

        var tag = value[1..^1];
        var dash = tag.IndexOf('-');
        if (dash > 0)
        {
            tag = tag[..dash];
        }

        return uint.TryParse(tag, NumberStyles.None, CultureInfo.InvariantCulture, out var version) ? version : null;
    }
}
