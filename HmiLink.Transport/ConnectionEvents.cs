using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HmiLink.Transport;

public enum HmiLinkSide { HmiClient, ExternalServer }
public enum HmiLinkConnectionState { Connecting, Connected, Reconnecting, Disconnected }

public sealed record HmiLinkConnectionEvent(
    HmiLinkSide Side,
    string PeerId,
    HmiLinkConnectionState State,
    string? ConnectionId,
    string? Error,
    DateTimeOffset OccurredAtUtc);

public interface IHmiLinkConnectionHandler
{
    Task HandleAsync(HmiLinkConnectionEvent change, CancellationToken ct);
}

internal sealed class ConnectionNotifier
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ConnectionNotifier> _log;

    public ConnectionNotifier(IServiceScopeFactory scopes, ILogger<ConnectionNotifier> log)
    {
        _scopes = scopes;
        _log = log;
    }

    public async Task NotifyAsync(HmiLinkConnectionEvent change, CancellationToken ct = default)
    {
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            foreach (var handler in scope.ServiceProvider.GetServices<IHmiLinkConnectionHandler>())
            {
                try
                {
                    await handler.HandleAsync(change, ct);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Connection observer failed for {PeerId} {State}", change.PeerId, change.State);
                }
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Unable to notify connection observers for {PeerId}", change.PeerId);
        }
    }
}
