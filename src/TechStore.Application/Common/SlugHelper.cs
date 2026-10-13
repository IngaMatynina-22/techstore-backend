using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TechStore.Application.Common;

public static partial class SlugHelper
{
    public static string Generate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(ch switch
            {
                >= 'a' and <= 'z' => ch,
                >= '0' and <= '9' => ch,
                ' ' or '_' or '-' => '-',
                _ => '-'
            });
        }

        var slug = CollapseHyphens().Replace(builder.ToString(), "-").Trim('-');
        return slug;
    }

    public static bool IsValid(string slug) =>
        !string.IsNullOrWhiteSpace(slug) && ValidSlug().IsMatch(slug);

    [GeneratedRegex("-{2,}")]
    private static partial Regex CollapseHyphens();

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex ValidSlug();
}
