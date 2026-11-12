using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HmiLink.Transport.Server;

public sealed class HmiLinkHub : Hub
{
    public const string EventMethod = "HmiLinkEvent";

    private readonly CommandRegistry _commands;
    private readonly IServiceScopeFactory _scopes;
    private readonly ConnectionNotifier _connectionEvents;
    private readonly ILogger<HmiLinkHub> _logger;

    public HmiLinkHub(IServiceProvider services, IServiceScopeFactory scopes,
        ILogger<HmiLinkHub> logger)
    {
        _commands = services.GetRequiredService<CommandRegistry>();
        _scopes = scopes;
        _connectionEvents = services.GetRequiredService<ConnectionNotifier>();
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        await _connectionEvents.NotifyAsync(ConnectionEvent(HmiLinkConnectionState.Connected));
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await _connectionEvents.NotifyAsync(ConnectionEvent(
            HmiLinkConnectionState.Disconnected, exception?.Message));
        await base.OnDisconnectedAsync(exception);
    }

    private HmiLinkConnectionEvent ConnectionEvent(HmiLinkConnectionState state, string? error = null) =>
        new(HmiLinkSide.ExternalServer, Context.UserIdentifier ?? Context.ConnectionId,
            state, Context.ConnectionId, error, DateTimeOffset.UtcNow);

    // Stable hub method; class-based message type selects an allow-listed command handler.
    public async Task<CommandReply> Execute(CommandEnvelope command, CancellationToken ct)
    {
        if (command is null || !_commands.Has(command.Name))
            return new CommandReply(command?.MessageId ?? Guid.Empty, false, null, "UnknownCommand");

        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var result = await _commands.DispatchAsync(command.Name, scope.ServiceProvider, command.Payload, ct);
            return new CommandReply(command.MessageId, true, result, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Command {CommandName} ({CommandId}) failed", command.Name, command.MessageId);
            return new CommandReply(command.MessageId, false, null, "CommandFailed");
        }
    }
}
