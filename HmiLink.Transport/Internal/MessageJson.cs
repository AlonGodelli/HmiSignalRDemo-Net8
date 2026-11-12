using System.Text.Json;

namespace HmiLink.Transport.Internal;

internal static class MessageJson
{
    // Consistent DTO encoding on both ends; independent of the outer SignalR protocol serializer.
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static JsonElement Write<T>(T payload) => JsonSerializer.SerializeToElement(payload, Options);

    public static T Read<T>(JsonElement payload) =>
        payload.Deserialize<T>(Options) ??
        throw new JsonException($"Unable to deserialize {typeof(T).Name}");
}
