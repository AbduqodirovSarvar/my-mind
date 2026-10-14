using System.Text.Json;
using FluentValidation;
using MyMind.Domain.Articles;
using MyMind.Domain.Common;

namespace MyMind.Application.Features.Articles;

public sealed record CreateArticleRequest(Guid CategoryId, string? Title);

/// <param name="Content">The TipTap document (<c>{"type":"doc",...}</c>).</param>
public sealed record SaveDraftRequest(string Title, JsonElement Content);

/// <param name="Excerpt">A hand-written summary; null or empty means "generate it on publish".</param>
public sealed record UpdateArticleMetaRequest(Guid CategoryId, string Slug, string? Excerpt);

public sealed record ArticleListItem(
    Guid Id,
    Guid CategoryId,
    string Slug,
    ArticleStatus Status,
    string Title,
    DateTimeOffset? PublishedAt,
    DateTimeOffset UpdatedAt);

/// <param name="Version">Concurrency token; the API also sends it as the ETag.</param>
public sealed record ArticleDetailResponse(
    Guid Id,
    Guid CategoryId,
    string Slug,
    ArticleStatus Status,
    string DraftTitle,
    JsonElement DraftContent,
    DateTimeOffset DraftUpdatedAt,
    string? PublishedTitle,
    string? Excerpt,
    bool HasCustomExcerpt,
    DateTimeOffset? PublishedAt,
    bool HasUnpublishedChanges,
    DateTimeOffset UpdatedAt,
    uint Version);

public sealed record RevisionResponse(Guid Id, RevisionKind Kind, string Title, DateTimeOffset CreatedAt);

public sealed class CreateArticleRequestValidator : AbstractValidator<CreateArticleRequest>
{
    public CreateArticleRequestValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Title).MaximumLength(Article.TitleMaxLength);
    }
}

public sealed class SaveDraftRequestValidator : AbstractValidator<SaveDraftRequest>
{
    /// <summary>Plan §6.2: an article's JSON is at most 2 MB.</summary>
    public const int MaxContentBytes = 2 * 1024 * 1024;

    public SaveDraftRequestValidator()
    {
        RuleFor(x => x.Title).NotNull().MaximumLength(Article.TitleMaxLength);
        RuleFor(x => x.Content)
            .Must(c => c.ValueKind == JsonValueKind.Object
                && c.TryGetProperty("type", out var type)
                && type.ValueKind == JsonValueKind.String
                && type.GetString() == "doc")
            .WithMessage("Kontent TipTap hujjati bo'lishi kerak.")
            .Must(c => c.ValueKind == JsonValueKind.Undefined || c.GetRawText().Length <= MaxContentBytes)
            .WithMessage("Maqola hajmi 2 MB dan oshmasligi kerak.");
    }
}

public sealed class UpdateArticleMetaRequestValidator : AbstractValidator<UpdateArticleMetaRequest>
{
    public UpdateArticleMetaRequestValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Slug)
            .NotEmpty()
            .Must(Slug.IsValid).WithMessage("Slug faqat kichik lotin harflari, raqamlar va chiziqchadan iborat bo'lishi kerak.");
        RuleFor(x => x.Excerpt).MaximumLength(Article.ExcerptMaxLength);
    }
}
