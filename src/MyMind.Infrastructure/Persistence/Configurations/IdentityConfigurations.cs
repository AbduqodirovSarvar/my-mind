using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyMind.Application.Features.Auth;
using MyMind.Domain.Users;
using MyMind.Infrastructure.Identity;

namespace MyMind.Infrastructure.Persistence.Configurations;

internal sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("users");
        builder.Property(u => u.UserName).HasMaxLength(UsernameRules.MaxLength);
        builder.Property(u => u.NormalizedUserName).HasMaxLength(UsernameRules.MaxLength);
        builder.Property(u => u.DisplayName).HasMaxLength(RegisterRequestValidator.DisplayNameMaxLength).IsRequired();
        builder.Property(u => u.Bio).HasMaxLength(500);

        builder.HasIndex(u => u.NormalizedUserName).HasDatabaseName("ix_users_normalized_user_name").IsUnique();
        // Identity only checks email uniqueness in code; the index closes the race between two sign-ups.
        builder.HasIndex(u => u.NormalizedEmail).HasDatabaseName("ix_users_normalized_email").IsUnique();
    }
}

internal sealed class AppRoleConfiguration : IEntityTypeConfiguration<AppRole>
{
    // Fixed ids so the seed data is stable across migrations.
    private static readonly (Guid Id, string Name)[] Roles =
    [
        (new Guid("0199e2a0-0000-7000-8000-000000000001"), AppRole.User),
        (new Guid("0199e2a0-0000-7000-8000-000000000002"), AppRole.Moderator),
        (new Guid("0199e2a0-0000-7000-8000-000000000003"), AppRole.Admin),
    ];

    public void Configure(EntityTypeBuilder<AppRole> builder)
    {
        builder.ToTable("roles");
        builder.HasIndex(r => r.NormalizedName).HasDatabaseName("ix_roles_normalized_name").IsUnique();
        builder.HasData(Roles.Select(r => new AppRole
        {
            Id = r.Id,
            Name = r.Name,
            NormalizedName = r.Name.ToUpperInvariant(),
            ConcurrencyStamp = r.Id.ToString(),
        }));
    }
}

internal sealed class IdentityTablesConfiguration :
    IEntityTypeConfiguration<IdentityUserRole<Guid>>,
    IEntityTypeConfiguration<IdentityUserClaim<Guid>>,
    IEntityTypeConfiguration<IdentityUserLogin<Guid>>,
    IEntityTypeConfiguration<IdentityUserToken<Guid>>,
    IEntityTypeConfiguration<IdentityRoleClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserRole<Guid>> builder) => builder.ToTable("user_roles");

    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> builder) => builder.ToTable("user_claims");

    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder) => builder.ToTable("user_logins");

    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder) => builder.ToTable("user_tokens");

    public void Configure(EntityTypeBuilder<IdentityRoleClaim<Guid>> builder) => builder.ToTable("role_claims");
}
