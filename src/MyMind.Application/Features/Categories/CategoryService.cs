using Microsoft.EntityFrameworkCore;
using MyMind.Application.Common;
using MyMind.Domain;
using MyMind.Domain.Categories;
using MyMind.Domain.Common;

namespace MyMind.Application.Features.Categories;

/// <summary>The signed-in author's own category tree. Every query is scoped to <see cref="ICurrentUser"/>.</summary>
public sealed class CategoryService(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
{
    private const string NotFoundMessage = "Kategoriya topilmadi.";
    private const string SlugFallback = "kategoriya";

    public async Task<IReadOnlyList<CategoryResponse>> GetTreeAsync(CancellationToken ct)
    {
        var userId = currentUser.Id;
        var categories = await db.Categories.AsNoTracking().Where(c => c.UserId == userId).ToListAsync(ct);
        var articleCounts = await db.Articles
            .Where(a => a.UserId == userId)
            .GroupBy(a => a.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count, ct);

        return BuildTree(categories, articleCounts);
    }

    public async Task<Result<CategoryResponse>> CreateAsync(CreateCategoryRequest request, CancellationToken ct)
    {
        var userId = currentUser.Id;
        var categories = await db.Categories.Where(c => c.UserId == userId).ToListAsync(ct);
        if (categories.Count >= CategoryTree.MaxCategoriesPerUser)
        {
            return Error.Validation($"Kategoriyalar soni {CategoryTree.MaxCategoriesPerUser} tadan oshmasligi kerak.");
        }

        Category? parent = null;
        if (request.ParentId is { } parentId)
        {
            parent = categories.FirstOrDefault(c => c.Id == parentId);
            if (parent is null)
            {
                return new Error(ErrorKind.NotFound, "Ota kategoriya topilmadi.", "parentId");
            }
        }

        var siblings = categories.Where(c => c.ParentId == request.ParentId).ToList();
        var slug = Slug.MakeUnique(Slug.From(request.Name, SlugFallback), siblings.Select(c => c.Slug).ToHashSet());
        var sortOrder = siblings.Count == 0 ? 0 : siblings.Max(c => c.SortOrder) + 1;

        Category category;
        try
        {
            category = Category.Create(userId, parent, request.Name, slug, sortOrder, clock.GetUtcNow());
        }
        catch (DomainException e)
        {
            return Error.Validation(e.Message, "parentId");
        }

        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);

        return ToResponse(category, articleCount: 0, []);
    }

    public async Task<Result<CategoryResponse>> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken ct)
    {
        var userId = currentUser.Id;
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);
        if (category is null)
        {
            return Error.NotFound(NotFoundMessage);
        }

        var siblingSlugs = await db.Categories
            .Where(c => c.UserId == userId && c.ParentId == category.ParentId && c.Id != id)
            .Select(c => c.Slug)
            .ToListAsync(ct);

        string slug;
        if (request.Slug is not null)
        {
            if (siblingSlugs.Contains(request.Slug))
            {
                return Error.Conflict("Bu slug shu darajada band.", "slug");
            }

            slug = request.Slug;
        }
        else
        {
            // Category slugs only decorate article URLs (articles are found by their own slug),
            // so following the name is safe; old links reach the canonical URL by redirect.
            slug = Slug.MakeUnique(Slug.From(request.Name, SlugFallback), siblingSlugs.ToHashSet());
        }

        category.Rename(request.Name, slug, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);

        var articleCount = await db.Articles.CountAsync(a => a.CategoryId == id, ct);
        return ToResponse(category, articleCount, []);
    }

    public async Task<Result<Success>> DeleteAsync(Guid id, CancellationToken ct)
    {
        var userId = currentUser.Id;
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);
        if (category is null)
        {
            return Error.NotFound(NotFoundMessage);
        }

        if (await db.Categories.AnyAsync(c => c.ParentId == id, ct))
        {
            return Error.Conflict("Avval ichidagi subkategoriyalarni o'chiring yoki ko'chiring.");
        }

        if (await db.Articles.AnyAsync(a => a.CategoryId == id, ct))
        {
            return Error.Conflict("Avval ichidagi maqolalarni o'chiring yoki ko'chiring.");
        }

        // Soft-deleted articles still reference the category; they go with it.
        await db.Articles.IgnoreQueryFilters()
            .Where(a => a.CategoryId == id && a.DeletedAt != null)
            .ExecuteDeleteAsync(ct);

        db.Categories.Remove(category);
        await db.SaveChangesAsync(ct);

        return new Success();
    }

    public async Task<Result<IReadOnlyList<CategoryResponse>>> ReorderAsync(ReorderCategoriesRequest request, CancellationToken ct)
    {
        var userId = currentUser.Id;
        var categories = await db.Categories.Where(c => c.UserId == userId).ToDictionaryAsync(c => c.Id, ct);

        // Someone else's category — as an item or as a new parent — is reported like a missing one.
        if (request.Items.Any(i => !categories.ContainsKey(i.Id) || (i.ParentId is { } p && !categories.ContainsKey(p))))
        {
            return Error.NotFound(NotFoundMessage);
        }

        var moves = request.Items.ToDictionary(i => i.Id);
        var proposed = categories.Values
            .Select(c => (c.Id, moves.TryGetValue(c.Id, out var move) ? move.ParentId : c.ParentId, c.Slug))
            .ToList();

        if (CategoryTree.Validate(proposed) is { } violation)
        {
            return Error.Validation(violation);
        }

        var now = clock.GetUtcNow();
        foreach (var item in request.Items)
        {
            categories[item.Id].Reposition(item.ParentId, item.SortOrder, now);
        }

        await db.SaveChangesAsync(ct);

        return Result<IReadOnlyList<CategoryResponse>>.From(await GetTreeAsync(ct));
    }

    private static IReadOnlyList<CategoryResponse> BuildTree(List<Category> categories, Dictionary<Guid, int> articleCounts)
    {
        IReadOnlyList<CategoryResponse> ChildrenOf(Guid? parentId) => categories
            .Where(c => c.ParentId == parentId)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => ToResponse(c, articleCounts.GetValueOrDefault(c.Id), parentId is null ? ChildrenOf(c.Id) : []))
            .ToList();

        return ChildrenOf(null);
    }

    private static CategoryResponse ToResponse(Category c, int articleCount, IReadOnlyList<CategoryResponse> children) =>
        new(c.Id, c.ParentId, c.Name, c.Slug, c.SortOrder, articleCount, children);
}
