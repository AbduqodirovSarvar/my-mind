using Microsoft.AspNetCore.Identity;

namespace MyMind.Infrastructure.Identity;

public sealed class AppUser : IdentityUser<Guid>
{
    public AppUser()
    {
        Id = Guid.CreateVersion7();
        SecurityStamp = Guid.NewGuid().ToString();
    }

    public string DisplayName { get; set; } = "";

    public string? Bio { get; set; }

    public bool IsBlocked { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the user accepted the terms of use (legal requirement, plan §11).</summary>
    public DateTimeOffset TermsAcceptedAt { get; set; }
}
