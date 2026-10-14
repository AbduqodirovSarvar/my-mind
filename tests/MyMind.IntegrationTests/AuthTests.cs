using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using MyMind.Application.Features.Auth;
using MyMind.IntegrationTests.Infrastructure;

namespace MyMind.IntegrationTests;

/// <summary>Phase 1 gate: register → login → logout with CSRF protection.</summary>
public class AuthTests(ApiFactory factory)
{
    private const string Password = "correct-horse-battery";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Register_SignsIn_AndMeReturnsTheUser()
    {
        var client = await StartSessionAsync();
        var request = NewRegistration();

        var register = await client.PostAsJsonAsync("/api/auth/register", request, Ct);

        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var me = await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me", Ct);
        Assert.Equal(request.UserName, me!.UserName);
        Assert.Equal(request.Email, me.Email);
        Assert.False(me.EmailConfirmed);
    }

    [Fact]
    public async Task Logout_EndsTheSession()
    {
        var client = await RegisteredClientAsync();

        var logout = await client.PostAsync("/api/auth/logout", null, Ct);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        var me = await client.GetAsync("/api/auth/me", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Login_WithUsernameOrEmail_StartsASession(bool useEmail)
    {
        var request = NewRegistration();
        await (await StartSessionAsync()).PostAsJsonAsync("/api/auth/register", request, Ct);
        var client = await StartSessionAsync();

        var login = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest(useEmail ? request.Email : request.UserName.ToUpperInvariant(), Password), Ct);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var me = await client.GetAsync("/api/auth/me", Ct);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var request = NewRegistration();
        await (await StartSessionAsync()).PostAsJsonAsync("/api/auth/register", request, Ct);
        var client = await StartSessionAsync();

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(request.UserName, "wrong-password-123"), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Login_LocksTheAccount_AfterFiveFailures()
    {
        var request = NewRegistration();
        await (await StartSessionAsync()).PostAsJsonAsync("/api/auth/register", request, Ct);
        var client = await StartSessionAsync();

        for (var i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(request.UserName, "wrong-password-123"), Ct);
        }

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(request.UserName, Password), Ct);

        Assert.Equal(HttpStatusCode.Locked, login.StatusCode);
    }

    [Fact]
    public async Task StateChangingRequest_WithoutXsrfHeader_IsRejected()
    {
        var client = ApiFactory.CreateBrowserClient(factory, sendXsrfHeader: false);
        await client.GetAsync("/api/auth/me", Ct);

        var register = await client.PostAsJsonAsync("/api/auth/register", NewRegistration(), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, register.StatusCode);
        var problem = await register.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
        Assert.Contains("CSRF", problem!.Title);
    }

    [Fact]
    public async Task Logout_WithTokenIssuedBeforeLogin_IsRejected()
    {
        // The token is bound to the user; a token from the anonymous session must not work after sign-in.
        var client = ApiFactory.CreateBrowserClient(factory, sendXsrfHeader: false);
        var anonymous = await client.GetAsync("/api/auth/me", Ct);
        var staleToken = ReadXsrfCookie(anonymous);
        using var register = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register")
        {
            Content = JsonContent.Create(NewRegistration()),
            Headers = { { "X-XSRF-TOKEN", staleToken } },
        };
        Assert.Equal(HttpStatusCode.Created, (await client.SendAsync(register, Ct)).StatusCode);

        using var logout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout")
        {
            Headers = { { "X-XSRF-TOKEN", staleToken } },
        };
        var response = await client.SendAsync(logout, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithTakenUsername_ReturnsValidationProblem()
    {
        var first = NewRegistration();
        await (await StartSessionAsync()).PostAsJsonAsync("/api/auth/register", first, Ct);
        var client = await StartSessionAsync();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register", NewRegistration() with { UserName = first.UserName.ToUpperInvariant() }, Ct);

        await AssertValidationErrorAsync(response, "userName");
    }

    [Fact]
    public async Task Register_WithTakenEmail_ReturnsValidationProblem()
    {
        var first = NewRegistration();
        await (await StartSessionAsync()).PostAsJsonAsync("/api/auth/register", first, Ct);
        var client = await StartSessionAsync();

        var response = await client.PostAsJsonAsync("/api/auth/register", NewRegistration() with { Email = first.Email }, Ct);

        await AssertValidationErrorAsync(response, "email");
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("ab")]
    [InlineData("has-dash")]
    public async Task Register_WithInvalidUsername_ReturnsValidationProblem(string userName)
    {
        var client = await StartSessionAsync();

        var response = await client.PostAsJsonAsync("/api/auth/register", NewRegistration() with { UserName = userName }, Ct);

        await AssertValidationErrorAsync(response, "UserName");
    }

    [Fact]
    public async Task Register_WithoutAcceptingTerms_ReturnsValidationProblem()
    {
        var client = await StartSessionAsync();

        var response = await client.PostAsJsonAsync("/api/auth/register", NewRegistration() with { AcceptTerms = false }, Ct);

        await AssertValidationErrorAsync(response, "AcceptTerms");
    }

    [Fact]
    public async Task Login_IsRateLimitedPerClient()
    {
        await using var limited = factory.WithWebHostBuilder(b => b.UseSetting("RateLimits:Login:PermitLimit", "2"));
        var client = ApiFactory.CreateBrowserClient(limited);
        await client.GetAsync("/api/auth/me", Ct);
        var request = new LoginRequest("nobody", "wrong-password-123");

        await client.PostAsJsonAsync("/api/auth/login", request, Ct);
        await client.PostAsJsonAsync("/api/auth/login", request, Ct);
        var third = await client.PostAsJsonAsync("/api/auth/login", request, Ct);

        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }

    private async Task<HttpClient> StartSessionAsync()
    {
        var client = factory.CreateBrowserClient();
        // What the Angular app does on startup: picks up the XSRF token.
        await client.GetAsync("/api/auth/me", Ct);
        return client;
    }

    private async Task<HttpClient> RegisteredClientAsync()
    {
        var client = await StartSessionAsync();
        var response = await client.PostAsJsonAsync("/api/auth/register", NewRegistration(), Ct);
        response.EnsureSuccessStatusCode();
        return client;
    }

    private static RegisterRequest NewRegistration()
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        return new RegisterRequest($"user{id}@example.com", $"u_{id}", Password, DisplayName: null, AcceptTerms: true);
    }

    private static string ReadXsrfCookie(HttpResponseMessage response)
    {
        var header = response.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));
        return header["XSRF-TOKEN=".Length..header.IndexOf(';')];
    }

    private static async Task AssertValidationErrorAsync(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Ct);
        Assert.Contains(problem!.Errors.Keys, key => key.Equals(field, StringComparison.OrdinalIgnoreCase));
    }
}
