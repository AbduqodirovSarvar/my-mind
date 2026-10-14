namespace MyMind.Domain.Categories;

/// <summary>
/// A node of an author's two-level tree: a root category (no parent) or a subcategory.
/// Slugs are unique among siblings of the same author.
/// </summary>
public sealed class Category
{
    public const int NameMaxLength = 80;

    private Category()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid? ParentId { get; private set; }

    public string Name { get; private set; } = "";

    public string Slug { get; private set; } = "";

    public int SortOrder { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsRoot => ParentId is null;

    public static Category Create(Guid userId, Category? parent, string name, string slug, int sortOrder, DateTimeOffset now)
    {
        EnsureParentAllowed(parent, userId);

        return new Category
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            ParentId = parent?.Id,
            Name = name.Trim(),
            Slug = slug,
            SortOrder = sortOrder,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public void Rename(string name, string slug, DateTimeOffset now)
    {
        Name = name.Trim();
        Slug = slug;
        UpdatedAt = now;
    }

    /// <summary>
    /// Places the category under <paramref name="parentId"/> (or at the root). A reorder moves many
    /// categories at once, so the two-level rule is checked on the whole resulting tree by
    /// <see cref="CategoryTree.Validate"/> before this is called.
    /// </summary>
    public void Reposition(Guid? parentId, int sortOrder, DateTimeOffset now)
    {
        ParentId = parentId;
        SortOrder = sortOrder;
        UpdatedAt = now;
    }

    /// <summary>The tree has exactly two levels: a parent must itself be a root of the same author.</summary>
    private static void EnsureParentAllowed(Category? parent, Guid userId)
    {
        if (parent is null)
        {
            return;
        }

        if (parent.UserId != userId)
        {
            throw new DomainException("Kategoriya topilmadi.");
        }

        if (!parent.IsRoot)
        {
            throw new DomainException("Faqat ikki daraja ruxsat etiladi: subkategoriya ichida kategoriya yaratib bo'lmaydi.");
        }
    }
}
