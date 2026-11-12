using HmiLink.Transport.Internal;
using Microsoft.AspNetCore.SignalR;

namespace HmiLink.Transport.Server;

public interface IHmiLinkPublisher
{
    Task PublishAsync<T>(T payload, CancellationToken ct = default);
    Task PublishAsync<T>(string eventName, T payload, CancellationToken ct = default);
}

internal sealed class HmiLinkPublisher : IHmiLinkPublisher
{
    private readonly IHubContext<HmiLinkHub> _hub;

    public HmiLinkPublisher(IHubContext<HmiLinkHub> hub) => _hub = hub;

    public Task PublishAsync<T>(T payload, CancellationToken ct = default) =>
        PublishAsync(HmiLinkMessageNames.For<T>(), payload, ct);

    public Task PublishAsync<T>(string eventName, T payload, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(eventName)) throw new ArgumentException("Event name is required", nameof(eventName));
        var envelope = new EventEnvelope(Guid.NewGuid(), eventName, MessageJson.Write(payload), DateTimeOffset.UtcNow);
        // Broadcast is suitable when each External serves only its authorized HMI client.
        // For multi-tenant systems replace Clients.All with authenticated targeted recipients.
        return _hub.Clients.All.SendCoreAsync(HmiLinkHub.EventMethod, [envelope], ct);
    }
}
