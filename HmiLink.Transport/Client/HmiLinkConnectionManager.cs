using System.Collections.Concurrent;
using HmiLink.Transport.Internal;
using HmiLink.Transport.Server;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HmiLink.Transport.Client;

internal sealed class HmiLinkConnectionManager : BackgroundService, IHmiLinkClient
{
    private readonly HmiLinkClientOptions _options;
    private readonly IServiceScopeFactory _scopes;
    private readonly EventRegistry _events;
    private readonly IHmiLinkTokenProvider _tokens;
    private readonly ConnectionNotifier _connectionEvents;
    private readonly ILogger<HmiLinkConnectionManager> _log;
    private readonly ConcurrentDictionary<string, HubConnection> _connections = new(StringComparer.Ordinal);

    public HmiLinkConnectionManager(
        IOptions<HmiLinkClientOptions> options,
        IServiceScopeFactory scopes,
        EventRegistry events,
        IHmiLinkTokenProvider tokens,
        ConnectionNotifier connectionEvents,
        ILogger<HmiLinkConnectionManager> log)
    {
        _options = options.Value;
        _scopes = scopes;
        _events = events;
        _tokens = tokens;
        _connectionEvents = connectionEvents;
        _log = log;
    }

    public bool IsConnected(string serverId) =>
        _connections.TryGetValue(serverId, out var conn) && conn.State == HubConnectionState.Connected;

    public Task<TResult> SendAsync<TCommand, TResult>(
        string serverId, TCommand command, CancellationToken ct = default) =>
        SendAsync<TCommand, TResult>(serverId, HmiLinkMessageNames.For<TCommand>(), command, ct);

    public async Task<IReadOnlyList<HmiLinkDeliveryResult<TResult>>> SendToAllAsync<TCommand, TResult>(
        TCommand command, CancellationToken ct = default)
    {
        // Attempt every configured endpoint independently, without automatically retrying commands.
        var tasks = _options.Servers.Select(async endpoint =>
        {
            try
            {
                var response = await SendAsync<TCommand, TResult>(endpoint.Id, command, ct);
                return new HmiLinkDeliveryResult<TResult>(endpoint.Id, true, response, null);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Broadcast command failed for {ServerId}", endpoint.Id);
                return new HmiLinkDeliveryResult<TResult>(endpoint.Id, false, default, ex.Message);
            }
        });

        return await Task.WhenAll(tasks);
    }

    public async Task<TResult> SendAsync<TCommand, TResult>(
        string serverId, string commandName, TCommand command, CancellationToken ct = default)
    {
        if (!_connections.TryGetValue(serverId, out var connection))
            throw new InvalidOperationException($"Unknown or uninitialized External server: {serverId}");
        if (connection.State != HubConnectionState.Connected)
            throw new InvalidOperationException($"External server is not connected: {serverId}");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.CommandTimeoutSeconds));

        var request = new CommandEnvelope(Guid.NewGuid(), commandName, MessageJson.Write(command));
        var response = await connection.InvokeAsync<CommandReply>("Execute", request, timeout.Token);

        if (response.MessageId != request.MessageId)
            throw new InvalidOperationException("Response correlation ID mismatch");
        if (!response.Success)
            throw new HmiLinkRemoteException(response.ErrorCode ?? "UnknownError");
        if (response.Payload is null)
            throw new InvalidOperationException("Missing response body");
        return MessageJson.Read<TResult>(response.Payload.Value);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var endpoints = _options.Servers;
        if (endpoints.Count == 0) throw new InvalidOperationException("No External servers configured");
        if (endpoints.Any(s => string.IsNullOrWhiteSpace(s.Id) || !Uri.TryCreate(s.Url, UriKind.Absolute, out _)))
            throw new InvalidOperationException("Each External server needs a valid ID and absolute URL");
        if (endpoints.Select(s => s.Id).Distinct(StringComparer.Ordinal).Count() != endpoints.Count)
            throw new InvalidOperationException("External server IDs must be unique");
        if (_options.InitialRetrySeconds <= 0 || _options.CommandTimeoutSeconds <= 0)
            throw new InvalidOperationException("Timeouts must be positive");

        await Task.WhenAll(endpoints.Select(endpoint => MaintainAsync(endpoint, stoppingToken)));
    }

    private async Task MaintainAsync(HmiLinkEndpoint endpoint, CancellationToken ct)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(endpoint.Url, http =>
            {
                http.AccessTokenProvider = () => _tokens.GetTokenAsync(endpoint.Id);
            })
            .WithAutomaticReconnect()
            .Build();

        if (!_connections.TryAdd(endpoint.Id, connection))
            throw new InvalidOperationException($"Duplicate server: {endpoint.Id}");

        // Handler is registered only once, survives reconnections.
        using var subscription = connection.On<EventEnvelope>(HmiLinkHub.EventMethod, async message =>
        {
            try
            {
                if (!_events.TryGet(message.Name, out var handler) || handler is null)
                {
                    _log.LogWarning("Unregistered event {EventName} from {ServerId}", message.Name, endpoint.Id);
                    return;
                }
                await using var scope = _scopes.CreateAsyncScope();
                await handler(scope.ServiceProvider, endpoint.Id, message.Payload, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception ex)
            {
                _log.LogError(ex, "Event processing failed ({EventName}) from {ServerId}", message.Name, endpoint.Id);
            }
        });

        connection.Reconnecting += exception =>
        {
            _log.LogWarning(exception, "Connection interrupted: {ServerId}", endpoint.Id);
            return NotifyConnectionAsync(endpoint.Id, HmiLinkConnectionState.Reconnecting,
                connection.ConnectionId, exception?.Message);
        };
        connection.Reconnected += connectionId =>
        {
            _log.LogInformation("Reconnected to {ServerId}; refresh application snapshot if necessary", endpoint.Id);
            return NotifyConnectionAsync(endpoint.Id, HmiLinkConnectionState.Connected, connectionId);
        };

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                Task OnClosed(Exception? error)
                {
                    closed.TrySetResult();
                    return NotifyConnectionAsync(endpoint.Id, HmiLinkConnectionState.Disconnected,
                        connection.ConnectionId, error?.Message);
                }
                connection.Closed += OnClosed;
                try
                {
                    await NotifyConnectionAsync(endpoint.Id, HmiLinkConnectionState.Connecting);
                    await connection.StartAsync(ct);
                    _log.LogInformation("Connected to {ServerId}", endpoint.Id);
                    await NotifyConnectionAsync(endpoint.Id, HmiLinkConnectionState.Connected,
                        connection.ConnectionId);
                    await closed.Task.WaitAsync(ct); // no remote polling
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Unable to connect to {ServerId}", endpoint.Id);
                    await NotifyConnectionAsync(endpoint.Id, HmiLinkConnectionState.Disconnected,
                        connection.ConnectionId, ex.Message);
                }
                finally
                {
                    connection.Closed -= OnClosed;
                }
                await Task.Delay(TimeSpan.FromSeconds(_options.InitialRetrySeconds), ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        finally
        {
            _connections.TryRemove(endpoint.Id, out _);
            await connection.DisposeAsync();
        }
    }

    private Task NotifyConnectionAsync(string serverId, HmiLinkConnectionState state,
        string? connectionId = null, string? error = null) =>
        _connectionEvents.NotifyAsync(new HmiLinkConnectionEvent(
            HmiLinkSide.HmiClient, serverId, state, connectionId, error, DateTimeOffset.UtcNow));
}
