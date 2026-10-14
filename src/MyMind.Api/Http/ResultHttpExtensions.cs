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

    public static uint? ParseIfMatch(HttpRequest request)
    {
        var header = request.Headers[HeaderNames.IfMatch].ToString().Trim();
        return header.Length > 2 && header[0] == '"' && header[^1] == '"'
            && uint.TryParse(header[1..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var version)
                ? version
                : null;
    }
}
