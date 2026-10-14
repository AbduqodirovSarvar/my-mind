using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace MyMind.Api.Http;

internal static class RateLimitPolicies
{
    public const string Login = "login";
    public const string Register = "register";
}

internal sealed class RateLimitOptions
{
    public const string Section = "RateLimits";

    public FixedWindow Login { get; set; } = new() { PermitLimit = 5, WindowSeconds = 60 };

    public FixedWindow Register { get; set; } = new() { PermitLimit = 3, WindowSeconds = 3600 };

    public sealed class FixedWindow
    {
        public int PermitLimit { get; set; }

        public int WindowSeconds { get; set; }
    }
}

internal static class RateLimitingExtensions
{
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.Section));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteProblemAsync;

            options.AddPolicy(RateLimitPolicies.Login, http => PerClientIp(http, o => o.Login));
            options.AddPolicy(RateLimitPolicies.Register, http => PerClientIp(http, o => o.Register));
        });

        return services;
    }

    // Behind Caddy the client IP comes from X-Forwarded-For via UseForwardedHeaders.
    private static RateLimitPartition<string> PerClientIp(
        HttpContext http, Func<RateLimitOptions, RateLimitOptions.FixedWindow> select)
    {
        var window = select(http.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value);
        var clientIp = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = window.PermitLimit,
            Window = TimeSpan.FromSeconds(window.WindowSeconds),
            QueueLimit = 0,
        });
    }

    private static async ValueTask WriteProblemAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
        }

        var problems = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context.HttpContext,
            ProblemDetails =
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Juda ko'p so'rov. Birozdan keyin qayta urinib ko'ring.",
            },
        });
    }
}
