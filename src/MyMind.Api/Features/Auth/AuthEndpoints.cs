using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using MyMind.Api.Http;
using MyMind.Application.Features.Auth;
using MyMind.Domain.Users;
using MyMind.Infrastructure.Identity;

namespace MyMind.Api.Features.Auth;

internal static class AuthEndpoints
{
    private const string InvalidCredentials = "Login yoki parol noto'g'ri.";

    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var auth = api.MapGroup("/auth").WithTags("Auth");

        auth.MapPost("/register", Register)
            .WithValidation<RegisterRequest>()
            .RequireRateLimiting(RateLimitPolicies.Register);

        auth.MapPost("/login", Login)
            .WithValidation<LoginRequest>()
            .RequireRateLimiting(RateLimitPolicies.Login);

        auth.MapPost("/logout", Logout)
            .RequireAuthorization();

        // Also the place where the browser app picks up its XSRF token on startup.
        auth.MapGet("/me", Me);

        return api;
    }

    private static async Task<Results<Created<CurrentUserResponse>, ValidationProblem>> Register(
        RegisterRequest request,
        UserManager<AppUser> users,
        SignInManager<AppUser> signIn,
        TimeProvider clock,
        HttpContext http)
    {
        var now = clock.GetUtcNow();
        var userName = UsernameRules.Normalize(request.UserName);
        var user = new AppUser
        {
            UserName = userName,
            Email = request.Email.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? userName : request.DisplayName.Trim(),
            CreatedAt = now,
            TermsAcceptedAt = now,
        };

        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            return TypedResults.ValidationProblem(ToValidationErrors(created));
        }

        await users.AddToRoleAsync(user, AppRole.User);
        await signIn.SignInAsync(user, isPersistent: true);
        await ReissueXsrfTokenAsync(http, signIn, user);

        return TypedResults.Created("/api/auth/me", ToResponse(user));
    }

    private static async Task<Results<Ok<CurrentUserResponse>, ProblemHttpResult>> Login(
        LoginRequest request,
        UserManager<AppUser> users,
        SignInManager<AppUser> signIn,
        HttpContext http)
    {
        var login = request.Login.Trim();
        var user = login.Contains('@')
            ? await users.FindByEmailAsync(login)
            : await users.FindByNameAsync(UsernameRules.Normalize(login));

        if (user is null)
        {
            return Problem(StatusCodes.Status401Unauthorized, InvalidCredentials);
        }

        var check = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (check.IsLockedOut)
        {
            return Problem(StatusCodes.Status423Locked, "Juda ko'p muvaffaqiyatsiz urinish. 15 daqiqadan keyin qayta urinib ko'ring.");
        }

        if (!check.Succeeded)
        {
            return Problem(StatusCodes.Status401Unauthorized, InvalidCredentials);
        }

        // Checked only after the password, so the blocked status is not revealed to strangers.
        if (user.IsBlocked)
        {
            return Problem(StatusCodes.Status403Forbidden, "Akkaunt bloklangan.");
        }

        await signIn.SignInAsync(user, request.RememberMe);
        await ReissueXsrfTokenAsync(http, signIn, user);

        return TypedResults.Ok(ToResponse(user));
    }

    private static async Task<NoContent> Logout(SignInManager<AppUser> signIn, HttpContext http)
    {
        await signIn.SignOutAsync();

        http.User = new ClaimsPrincipal(new ClaimsIdentity());
        XsrfToken.Issue(http);

        return TypedResults.NoContent();
    }

    /// <summary>The signed-in user, or 204 for anonymous visitors (not an error, so no 401 noise in the browser).</summary>
    private static async Task<Results<Ok<CurrentUserResponse>, NoContent>> Me(
        UserManager<AppUser> users,
        HttpContext http)
    {
        XsrfToken.Issue(http);

        var user = http.User.Identity?.IsAuthenticated == true ? await users.GetUserAsync(http.User) : null;

        return user is null
            ? TypedResults.NoContent()
            : TypedResults.Ok(ToResponse(user));
    }

    /// <summary>The antiforgery token is bound to the user, so a sign-in needs a fresh one.</summary>
    private static async Task ReissueXsrfTokenAsync(HttpContext http, SignInManager<AppUser> signIn, AppUser user)
    {
        http.User = await signIn.CreateUserPrincipalAsync(user);
        XsrfToken.Issue(http);
    }

    private static CurrentUserResponse ToResponse(AppUser user) =>
        new(user.Id, user.UserName!, user.Email!, user.DisplayName, user.EmailConfirmed);

    private static ProblemHttpResult Problem(int status, string title) =>
        TypedResults.Problem(statusCode: status, title: title);

    private static Dictionary<string, string[]> ToValidationErrors(IdentityResult result) =>
        result.Errors
            .GroupBy(e => e.Code switch
            {
                nameof(IdentityErrorDescriber.DuplicateUserName) or nameof(IdentityErrorDescriber.InvalidUserName) => "userName",
                nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.InvalidEmail) => "email",
                _ when e.Code.StartsWith("Password", StringComparison.Ordinal) => "password",
                _ => "",
            })
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.Code switch
                {
                    nameof(IdentityErrorDescriber.DuplicateUserName) => "Bu username band.",
                    nameof(IdentityErrorDescriber.DuplicateEmail) => "Bu email bilan akkaunt allaqachon mavjud.",
                    _ => e.Description,
                }).ToArray());
}
