using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MyMind.Domain.Common;

/// <summary>
/// URL slugs: lowercase ASCII words joined by hyphens. Uzbek and Russian text (Latin or Cyrillic)
/// is transliterated, so "Oʻzbekcha gʻoya" becomes "ozbekcha-goya" (plan §6.2).
/// </summary>
public static partial class Slug
{
    public const int MaxLength = 80;

    private static readonly Dictionary<char, string> Cyrillic = new()
    {
        ['а'] = "a",
        ['б'] = "b",
        ['в'] = "v",
        ['г'] = "g",
        ['д'] = "d",
        ['е'] = "e",
        ['ё'] = "yo",
        ['ж'] = "j",
        ['з'] = "z",
        ['и'] = "i",
        ['й'] = "y",
        ['к'] = "k",
        ['л'] = "l",
        ['м'] = "m",
        ['н'] = "n",
        ['о'] = "o",
        ['п'] = "p",
        ['р'] = "r",
        ['с'] = "s",
        ['т'] = "t",
        ['у'] = "u",
        ['ф'] = "f",
        ['х'] = "x",
        ['ц'] = "ts",
        ['ч'] = "ch",
        ['ш'] = "sh",
        ['щ'] = "sh",
        ['ъ'] = "",
        ['ы'] = "i",
        ['ь'] = "",
        ['э'] = "e",
        ['ю'] = "yu",
        ['я'] = "ya",
        ['ў'] = "o",
        ['қ'] = "q",
        ['ғ'] = "g",
        ['ҳ'] = "h",
    };

    /// <summary>Apostrophe variants used in Uzbek Latin (oʻ, gʻ, tutuq belgisi) — dropped.</summary>
    private const string Apostrophes = "'ʻʼ‘’`´";

    public static string From(string text, string fallback)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text.ToLowerInvariant())
        {
            if (Apostrophes.Contains(c))
            {
                continue;
            }

            if (Cyrillic.TryGetValue(c, out var latin))
            {
                builder.Append(latin);
            }
            else
            {
                builder.Append(c);
            }
        }

        // Strip diacritics (é → e), then collapse everything else into single hyphens.
        var ascii = new StringBuilder(builder.Length);
        foreach (var c in builder.ToString().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                ascii.Append(c);
            }
        }

        var slug = NonAlphanumeric().Replace(ascii.ToString(), "-").Trim('-');
        if (slug.Length > MaxLength)
        {
            slug = slug[..MaxLength];
            var lastHyphen = slug.LastIndexOf('-');
            slug = (lastHyphen > MaxLength / 2 ? slug[..lastHyphen] : slug).Trim('-');
        }

        return slug.Length == 0 ? fallback : slug;
    }

    public static bool IsValid(string slug) => slug.Length <= MaxLength && Format().IsMatch(slug);

    /// <summary>Returns <paramref name="slug"/>, or slug-2, slug-3, … — the first one not taken.</summary>
    public static string MakeUnique(string slug, IReadOnlySet<string> taken)
    {
        if (!taken.Contains(slug))
        {
            return slug;
        }

        for (var n = 2; ; n++)
        {
            var suffix = $"-{n}";
            var candidate = (slug.Length + suffix.Length > MaxLength ? slug[..(MaxLength - suffix.Length)] : slug) + suffix;
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex Format();
}
