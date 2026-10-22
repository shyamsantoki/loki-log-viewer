using System.Text.RegularExpressions;
using LokiLogViewer.Shared.Search;

namespace LokiLogViewer.Shared.Querying;

// Search is a flat list of "label equals value" conditions, always ANDed - Loki's own stream
// selector syntax ("{label="value", ...}") already is an AND list, so there's no translation
// to do beyond escaping and validating each matcher.
public static partial class LogQlBuilder
{
    [GeneratedRegex("^[a-zA-Z_][a-zA-Z0-9_]*$")]
    private static partial Regex LabelNamePattern();

    public static string BuildSelector(IReadOnlyList<LogCondition> conditions)
    {
        if (conditions.Count == 0)
        {
            throw new ArgumentException("At least one filter value is required.");
        }

        var matchers = conditions.Select(c =>
        {
            if (!LabelNamePattern().IsMatch(c.Label))
            {
                throw new ArgumentException($"'{c.Label}' is not a valid Loki label name.");
            }

            return $"{c.Label}=\"{EscapeValue(c.Value)}\"";
        });

        return "{" + string.Join(", ", matchers) + "}";
    }

    private static string EscapeValue(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
