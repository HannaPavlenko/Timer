using System.Text.Json.Serialization;

namespace Timer.Core;

[JsonSourceGenerationOptions(RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(List<TimerHistoryEntry>))]
[JsonSerializable(typeof(bool))]
internal sealed partial class StorageJsonContext : JsonSerializerContext
{
}
