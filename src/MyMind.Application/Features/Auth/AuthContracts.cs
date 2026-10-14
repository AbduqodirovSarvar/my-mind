namespace MyMind.Application.Features.Auth;

public sealed record RegisterRequest(
    string Email,
    string UserName,
    string Password,
    string? DisplayName,
    bool AcceptTerms);

/// <param name="Login">Email or username.</param>
public sealed record LoginRequest(string Login, string Password, bool RememberMe = true);

public sealed record CurrentUserResponse(
    Guid Id,
    string UserName,
    string Email,
    string DisplayName,
    bool EmailConfirmed);
