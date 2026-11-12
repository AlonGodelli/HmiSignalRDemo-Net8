namespace HmiLink.Transport.Client;

public sealed record HmiLinkDeliveryResult<TResult>(
    string ServerId,
    bool Succeeded,
    TResult? Response,
    string? Error);

public interface IHmiLinkClient
{
    // Class-based API: the message type identifies the command.
    Task<TResult> SendAsync<TCommand, TResult>(
        string serverId, TCommand command, CancellationToken ct = default);

    // Sends concurrently to ALL configured servers, including reporting disconnected ones.
    Task<IReadOnlyList<HmiLinkDeliveryResult<TResult>>> SendToAllAsync<TCommand, TResult>(
        TCommand command, CancellationToken ct = default);

    // Optional explicit name, for legacy callers / two operations sharing the same DTO type.
    Task<TResult> SendAsync<TCommand, TResult>(
        string serverId, string commandName, TCommand command, CancellationToken ct = default);

    bool IsConnected(string serverId);
}

public interface IHmiLinkEventHandler<in TEvent>
{
    Task HandleAsync(string serverId, TEvent message, CancellationToken ct);
}

// Override in the HMI host to provide per-server service credentials.
public interface IHmiLinkTokenProvider
{
    Task<string?> GetTokenAsync(string serverId);
}

internal sealed class EmptyTokenProvider : IHmiLinkTokenProvider
{
    public Task<string?> GetTokenAsync(string serverId) => Task.FromResult<string?>(null);
}

public sealed class HmiLinkClientOptions
{
    public List<HmiLinkEndpoint> Servers { get; set; } = [];
    public int InitialRetrySeconds { get; set; } = 5;
    public int CommandTimeoutSeconds { get; set; } = 10;
}

public sealed class HmiLinkEndpoint
{
    public string Id { get; set; } = "";
    public string Url { get; set; } = "";
}
