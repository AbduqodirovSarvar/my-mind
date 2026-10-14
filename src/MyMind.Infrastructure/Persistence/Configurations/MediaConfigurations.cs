using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyMind.Domain.Articles;
using MyMind.Domain.Media;
using MyMind.Infrastructure.Identity;

namespace MyMind.Infrastructure.Persistence.Configurations;

internal sealed class MediaFileConfiguration : IEntityTypeConfiguration<MediaFile>
{
    public void Configure(EntityTypeBuilder<MediaFile> builder)
    {
        builder.ToTable("media");
        builder.Property(m => m.StorageKey).HasMaxLength(200);
        builder.Property(m => m.SmallStorageKey).HasMaxLength(200);
        builder.Property(m => m.ContentType).HasMaxLength(50);
        builder.Property(m => m.OriginalName).HasMaxLength(MediaFile.OriginalNameMaxLength);

        builder.HasOne<AppUser>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(m => m.StorageKey).IsUnique();
        builder.HasIndex(m => new { m.UserId, m.CreatedAt });
    }
}

internal sealed class ArticleMediaConfiguration : IEntityTypeConfiguration<ArticleMedia>
{
    public void Configure(EntityTypeBuilder<ArticleMedia> builder)
    {
        builder.ToTable("article_media");
        builder.HasKey(l => new { l.ArticleId, l.MediaId });

        builder.HasOne<Article>().WithMany().HasForeignKey(l => l.ArticleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MediaFile>().WithMany().HasForeignKey(l => l.MediaId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(l => l.MediaId);
    }
}
