using System.Text.Json;

namespace HmiLink.Transport;

// Only transport metadata is owned by the SDK. Business DTOs remain in your existing shared project.
public sealed record CommandEnvelope(Guid MessageId, string Name, JsonElement Payload);
public sealed record CommandReply(Guid MessageId, bool Success, JsonElement? Payload, string? ErrorCode);
public sealed record EventEnvelope(Guid MessageId, string Name, JsonElement Payload, DateTimeOffset SentAtUtc);

public sealed class HmiLinkRemoteException : Exception
{
    public string ErrorCode { get; }
    public HmiLinkRemoteException(string errorCode)
        : base($"Remote command failed: {errorCode}") => ErrorCode = errorCode;
}
