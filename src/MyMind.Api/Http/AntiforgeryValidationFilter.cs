using Microsoft.AspNetCore.Antiforgery;

namespace MyMind.Api.Http;

/// <summary>
/// Minimal API validates antiforgery tokens only for form posts, so every state-changing
/// <c>/api</c> request is checked here.
/// </summary>
internal sealed class AntiforgeryValidationFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;

        if (!IsSafeMethod(http.Request.Method))
        {
            try
            {
                await antiforgery.ValidateRequestAsync(http);
            }
            catch (AntiforgeryValidationException)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "CSRF token noto'g'ri yoki yuborilmagan.",
                    detail: $"So'rov {XsrfToken.HeaderName} sarlavhasini o'z ichiga olishi kerak.");
            }
        }

        return await next(context);
    }

    private static bool IsSafeMethod(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method) ||
        HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method);
}
