using System.Text.Json;
using System.Text.Json.Serialization;

namespace LokiLogViewer.Shared.Serialization;

// Server (HttpJsonOptions) and Client (its outgoing HttpClient calls) must both add this
// converter so enums like LabelOperator/GroupOperator serialize the same way on the wire.
public static class JsonDefaults
{
    public static void Apply(JsonSerializerOptions options) =>
        options.Converters.Add(new JsonStringEnumConverter());
}
