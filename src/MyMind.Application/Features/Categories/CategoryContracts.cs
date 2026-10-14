using FluentValidation;
using MyMind.Domain.Categories;
using MyMind.Domain.Common;

namespace MyMind.Application.Features.Categories;

public sealed record CategoryResponse(
    Guid Id,
    Guid? ParentId,
    string Name,
    string Slug,
    int SortOrder,
    int ArticleCount,
    IReadOnlyList<CategoryResponse> Children);

public sealed record CreateCategoryRequest(string Name, Guid? ParentId);

/// <param name="Slug">Optional; when omitted the slug follows the name.</param>
public sealed record UpdateCategoryRequest(string Name, string? Slug);

public sealed record CategoryPosition(Guid Id, Guid? ParentId, int SortOrder);

public sealed record ReorderCategoriesRequest(IReadOnlyList<CategoryPosition> Items);

public sealed class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Nom kiritilishi shart.").MaximumLength(Category.NameMaxLength);
    }
}

public sealed class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Nom kiritilishi shart.").MaximumLength(Category.NameMaxLength);
        RuleFor(x => x.Slug!)
            .Must(Slug.IsValid).WithMessage("Slug faqat kichik lotin harflari, raqamlar va chiziqchadan iborat bo'lishi kerak.")
            .When(x => x.Slug is not null);
    }
}

public sealed class ReorderCategoriesRequestValidator : AbstractValidator<ReorderCategoriesRequest>
{
    public ReorderCategoriesRequestValidator()
    {
        RuleFor(x => x.Items).NotEmpty().Must(items => items.Count <= CategoryTree.MaxCategoriesPerUser);
        RuleFor(x => x.Items)
            .Must(items => items.Select(i => i.Id).Distinct().Count() == items.Count)
            .WithMessage("Bir kategoriya bir necha marta berilgan.");
        RuleForEach(x => x.Items).ChildRules(item => item.RuleFor(i => i.SortOrder).GreaterThanOrEqualTo(0));
    }
}
