using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyMind.Domain.Articles;
using MyMind.Domain.Categories;
using MyMind.Domain.Common;
using MyMind.Infrastructure.Identity;

namespace MyMind.Infrastructure.Persistence.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");
        builder.Property(c => c.Name).HasMaxLength(Category.NameMaxLength);
        builder.Property(c => c.Slug).HasMaxLength(Slug.MaxLength);

        builder.HasOne<AppUser>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Category>().WithMany().HasForeignKey(c => c.ParentId).OnDelete(DeleteBehavior.Restrict);

        // NULLS NOT DISTINCT: root categories (parent_id IS NULL) must also have unique slugs (plan §5.2).
        builder.HasIndex(c => new { c.UserId, c.ParentId, c.Slug }).IsUnique().AreNullsDistinct(false);
        builder.HasIndex(c => new { c.UserId, c.ParentId, c.SortOrder });
    }
}

internal sealed class ArticleConfiguration : IEntityTypeConfiguration<Article>
{
    public void Configure(EntityTypeBuilder<Article> builder)
    {
        builder.ToTable("articles");
        builder.HasQueryFilter(a => a.DeletedAt == null);

        builder.Property(a => a.Slug).HasMaxLength(Slug.MaxLength);
        builder.Property(a => a.DraftTitle).HasMaxLength(Article.TitleMaxLength);
        builder.Property(a => a.Title).HasMaxLength(Article.TitleMaxLength);
        builder.Property(a => a.Excerpt).HasMaxLength(Article.ExcerptMaxLength);
        builder.Property(a => a.DraftJson).HasColumnType("jsonb");
        builder.Property(a => a.ContentJson).HasColumnType("jsonb");
        builder.Property(a => a.TocJson).HasColumnType("jsonb");

        // PostgreSQL's xmin system column as the concurrency token (ETag).
        builder.Property(a => a.Version).IsRowVersion();

        builder.HasOne<AppUser>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Category>().WithMany().HasForeignKey(a => a.CategoryId).OnDelete(DeleteBehavior.Restrict);

        // A deleted article frees its slug.
        builder.HasIndex(a => new { a.UserId, a.Slug }).IsUnique().HasFilter("deleted_at IS NULL");
        builder.HasIndex(a => new { a.UserId, a.Status, a.PublishedAt }).IsDescending(false, false, true);
        builder.HasIndex(a => a.CategoryId);
    }
}

internal sealed class ArticleRevisionConfiguration : IEntityTypeConfiguration<ArticleRevision>
{
    public void Configure(EntityTypeBuilder<ArticleRevision> builder)
    {
        builder.ToTable("article_revisions");
        builder.Property(r => r.Title).HasMaxLength(Article.TitleMaxLength);
        builder.Property(r => r.ContentJson).HasColumnType("jsonb");

        builder.HasOne<Article>().WithMany().HasForeignKey(r => r.ArticleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(r => new { r.ArticleId, r.Kind, r.CreatedAt });
    }
}

internal sealed class ArticleRedirectConfiguration : IEntityTypeConfiguration<ArticleRedirect>
{
    public void Configure(EntityTypeBuilder<ArticleRedirect> builder)
    {
        builder.ToTable("article_redirects");
        builder.Property(r => r.OldSlug).HasMaxLength(Slug.MaxLength);

        builder.HasOne<Article>().WithMany().HasForeignKey(r => r.ArticleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(r => new { r.UserId, r.OldSlug }).IsUnique();
    }
}
