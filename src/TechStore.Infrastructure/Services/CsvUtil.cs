using System.Globalization;
using System.Text;

namespace TechStore.Infrastructure.Services;

internal static class CsvUtil
{
    public const long MaxImportBytes = 2 * 1024 * 1024;
    public const int MaxImportRows = 5000;

    public static string SanitizeCell(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        var first = trimmed[0];
        if (first is '=' or '+' or '-' or '@')
        {
            return "'" + trimmed;
        }

        return trimmed;
    }

    public static string Escape(string? value)
    {
        var text = value ?? string.Empty;
        var needsQuotes = text.Contains(',') || text.Contains('"') || text.Contains('\r') || text.Contains('\n');
        if (!needsQuotes)
        {
            return text;
        }

        return "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    public static string EscapeAndSanitize(string? value) => Escape(SanitizeCell(value));

    public static async Task<List<string[]>> ReadRowsAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var rows = new List<string[]>();
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            rows.Add(ParseLine(line));
            if (rows.Count > MaxImportRows + 1)
            {
                break;
            }
        }

        return rows;
    }

    public static string[] ParseLine(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(ch);
                }
            }
            else
            {
                if (ch == ',')
                {
                    fields.Add(sb.ToString());
                    sb.Clear();
                }
                else if (ch == '"')
                {
                    inQuotes = true;
                }
                else
                {
                    sb.Append(ch);
                }
            }
        }

        fields.Add(sb.ToString());
        return fields.ToArray();
    }

    public static byte[] ToUtf8Bytes(StringBuilder builder) =>
        Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();

    public static string FormatDecimal(decimal value) =>
        value.ToString(CultureInfo.InvariantCulture);

    public static string FormatBool(bool value) => value ? "true" : "false";
}
