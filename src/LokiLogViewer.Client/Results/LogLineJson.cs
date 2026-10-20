using System.Text;
using System.Text.Json;

namespace LokiLogViewer.Client.Results;

// Many log lines are a JSON object (often followed by trailing garbage - some upstream log
// lines in practice contain more than one emission concatenated with no separator). This parses
// just the leading JSON object/value and ignores anything after it, rather than requiring the
// whole line to be valid JSON.
public static class LogLineJson
{
    private static readonly IReadOnlyDictionary<string, string?> Empty = new Dictionary<string, string?>();

    public static bool TryParseLeadingObject(string line, out JsonElement root)
    {
        root = default;

        var trimmed = line.AsSpan().TrimStart();
        if (trimmed.Length == 0 || trimmed[0] != '{')
        {
            return false;
        }

        var bytes = Encoding.UTF8.GetBytes(line);
        var reader = new Utf8JsonReader(bytes);
        try
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            root = doc.RootElement.Clone();
            return root.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    // Flattens a log line's JSON body into "dotted.path" -> value pairs (e.g.
    // "Resource.service.name"). Used both to discover which fields exist (the keys) and to read
    // one field's value (a lookup by the same key) - a single traversal shared by both, so a key
    // built here is never re-split back into segments. That matters because a JSON property can
    // itself legally contain a literal "." in its name (seen in real data as "service.name"),
    // which would otherwise be indistinguishable from two nested levels once joined into one
    // string. Arrays and primitives are treated as leaves.
    public static IReadOnlyDictionary<string, string?> Flatten(string line)
    {
        if (!TryParseLeadingObject(line, out var root))
        {
            return Empty;
        }

        var result = new Dictionary<string, string?>();
        FlattenInto(root, result, "");
        return result;
    }

    private static void FlattenInto(JsonElement element, Dictionary<string, string?> into, string prefix)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in element.EnumerateObject())
        {
            var path = prefix.Length == 0 ? property.Name : $"{prefix}.{property.Name}";
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                FlattenInto(property.Value, into, path);
            }
            else
            {
                into[path] = ToDisplayString(property.Value);
            }
        }
    }

    private static string? ToDisplayString(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        _ => value.GetRawText()
    };
}
