using System.Text.RegularExpressions;

namespace MyMind.Domain.Users;

/// <summary>
/// Usernames appear in public URLs (<c>/@username</c>), so they are lowercase, URL-safe
/// and must not shadow application routes.
/// </summary>
public static partial class UsernameRules
{
    public const int MinLength = 3;
    public const int MaxLength = 30;
    public const string AllowedCharacters = "abcdefghijklmnopqrstuvwxyz0123456789_";

    private static readonly HashSet<string> Reserved =
    [
        "about", "account", "admin", "administrator", "api", "app", "assets", "auth", "blog",
        "contact", "dashboard", "edit", "help", "home", "login", "logout", "me", "mymind",
        "new", "null", "privacy", "register", "root", "search", "settings", "signin", "signup",
        "sitemap", "static", "support", "system", "terms", "undefined", "uploads", "user", "users",
    ];

    public static string Normalize(string username) => username.Trim().ToLowerInvariant();

    public static bool IsValidFormat(string username) => FormatRegex().IsMatch(username);

    public static bool IsReserved(string username) => Reserved.Contains(Normalize(username));

    [GeneratedRegex("^[a-z0-9_]{3,30}$")]
    private static partial Regex FormatRegex();
}
