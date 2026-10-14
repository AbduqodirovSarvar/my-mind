using Microsoft.AspNetCore.Identity;

namespace MyMind.Infrastructure.Identity;

public sealed class AppRole : IdentityRole<Guid>
{
    public const string User = "User";
    public const string Moderator = "Moderator";
    public const string Admin = "Admin";

    public AppRole()
    {
        Id = Guid.CreateVersion7();
    }
}
