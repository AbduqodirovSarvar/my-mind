using Microsoft.EntityFrameworkCore;
using MyMind.Application.Common;
using MyMind.Infrastructure.Persistence;

namespace MyMind.Infrastructure.Identity;

internal sealed class UserDirectory(AppDbContext db) : IUserDirectory
{
    public Task<PublicUser?> FindPublicAsync(string userName, CancellationToken cancellationToken)
    {
        // Same normalization as Identity's UpperInvariantLookupNormalizer.
        var normalized = userName.Trim().ToUpperInvariant();

        return db.Users.AsNoTracking()
            .Where(u => u.NormalizedUserName == normalized && !u.IsBlocked)
            .Select(u => new PublicUser(u.Id, u.UserName!, u.DisplayName, u.Bio))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> IsEmailConfirmedAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(u => u.Id == userId && u.EmailConfirmed, cancellationToken);
}
