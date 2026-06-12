using System.Text.Json.Serialization;

namespace Chat.Shared;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ChatMessage))]
public sealed partial class ChatJsonContext : JsonSerializerContext;
