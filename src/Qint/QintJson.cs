using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qint;

/// <summary>
/// Shared <see cref="JsonSerializerOptions"/> for the SDK: camelCase properties
/// (matching the Qint API wire format), lowercase enum names, and null omission on write.
/// </summary>
internal static class QintJson
{
    public static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        // Enum members serialize as their lowercase name (Confirmed -> "confirmed"),
        // which matches the API's lowercase status vocabulary.
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
