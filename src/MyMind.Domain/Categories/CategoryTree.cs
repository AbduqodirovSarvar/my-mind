namespace MyMind.Domain.Categories;

/// <summary>Rules for an author's whole category tree.</summary>
public static class CategoryTree
{
    public const int MaxCategoriesPerUser = 500;

    /// <summary>
    /// Checks a (proposed) tree: every parent exists and is a root, so the tree has at most two
    /// levels, and slugs are unique among siblings. Returns the first violation, or null.
    /// </summary>
    public static string? Validate(IReadOnlyCollection<(Guid Id, Guid? ParentId, string Slug)> nodes)
    {
        var parentOf = nodes.ToDictionary(n => n.Id, n => n.ParentId);

        foreach (var node in nodes)
        {
            if (node.ParentId is not { } parentId)
            {
                continue;
            }

            if (parentId == node.Id || !parentOf.TryGetValue(parentId, out var grandParent))
            {
                return "Ota kategoriya topilmadi.";
            }

            if (grandParent is not null)
            {
                return "Faqat ikki daraja ruxsat etiladi: subkategoriya ichida kategoriya bo'lmaydi.";
            }
        }

        var duplicate = nodes
            .GroupBy(n => (n.ParentId, n.Slug))
            .FirstOrDefault(g => g.Count() > 1);

        return duplicate is null ? null : $"Bir joyda \"{duplicate.Key.Slug}\" nomli ikkita kategoriya bo'lib qoladi.";
    }
}
