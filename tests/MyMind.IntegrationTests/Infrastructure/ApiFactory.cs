using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(MyMind.IntegrationTests.Infrastructure.ApiFactory))]

namespace MyMind.IntegrationTests.Infrastructure;

/// <summary>
/// Runs the API against a real PostgreSQL in a container (shared by all tests; Docker required).
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17").Build();

    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync(TestContext.Current.CancellationToken);

        // Start the server (and migrate) once, here; test classes run in parallel and would
        // otherwise race to build the host and apply migrations concurrently.
        using var _ = CreateClient();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _database.GetConnectionString());
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("RateLimits:Login:PermitLimit", "1000");
        builder.UseSetting("RateLimits:Register:PermitLimit", "1000");
    }

    /// <summary>
    /// A browser-like client: keeps cookies and, like Angular's HttpClient, echoes the
    /// XSRF-TOKEN cookie in the X-XSRF-TOKEN header. HTTPS so that Secure cookies are sent.
    /// </summary>
    public HttpClient CreateBrowserClient(bool sendXsrfHeader = true) => CreateBrowserClient(this, sendXsrfHeader);

    public static HttpClient CreateBrowserClient(WebApplicationFactory<Program> factory, bool sendXsrfHeader = true)
    {
        var cookies = new CookieContainer();
        DelegatingHandler[] handlers = sendXsrfHeader
            ? [new XsrfHandler(cookies), new CookieContainerHandler(cookies)]
            : [new CookieContainerHandler(cookies)];

        return factory.CreateDefaultClient(new Uri("https://localhost"), handlers);
    }

    private sealed class XsrfHandler(CookieContainer cookies) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method != HttpMethod.Get && request.Method != HttpMethod.Head)
            {
                var token = cookies.GetCookies(request.RequestUri!)["XSRF-TOKEN"]?.Value;
                if (token is not null)
                {
                    request.Headers.Add("X-XSRF-TOKEN", token);
                }
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
