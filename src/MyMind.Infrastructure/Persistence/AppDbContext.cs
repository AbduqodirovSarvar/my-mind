using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MyMind.Application.Common;
using MyMind.Domain.Articles;
using MyMind.Domain.Categories;
using MyMind.Domain.Media;
using MyMind.Infrastructure.Identity;

namespace MyMind.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, AppRole, Guid,
        IdentityUserClaim<Guid>, IdentityUserRole<Guid>, IdentityUserLogin<Guid>,
        IdentityRoleClaim<Guid>, IdentityUserToken<Guid>>(options), IAppDbContext
{
    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Article> Articles => Set<Article>();

    public DbSet<ArticleRevision> ArticleRevisions => Set<ArticleRevision>();

    public DbSet<ArticleRedirect> ArticleRedirects => Set<ArticleRedirect>();

    public DbSet<MediaFile> Media => Set<MediaFile>();

    public DbSet<ArticleMedia> ArticleMedia => Set<ArticleMedia>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
