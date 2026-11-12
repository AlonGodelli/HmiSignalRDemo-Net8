using System.Text.Json;
using HmiLink.Transport.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace HmiLink.Transport.Server;

public interface IHmiLinkCommandHandler<TCommand, TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

internal sealed class CommandRegistry
{
    private readonly Dictionary<string, Func<IServiceProvider, JsonElement, CancellationToken, Task<JsonElement>>> _handlers =
        new(StringComparer.Ordinal);

    public bool Has(string name) => _handlers.ContainsKey(name);

    public void Add<TCommand, TResult, THandler>(string name)
        where THandler : class, IHmiLinkCommandHandler<TCommand, TResult>
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Command name is required", nameof(name));
        if (!_handlers.TryAdd(name, async (services, data, ct) =>
            MessageJson.Write(await services.GetRequiredService<THandler>()
                .HandleAsync(MessageJson.Read<TCommand>(data), ct))))
        {
            throw new InvalidOperationException($"Command already registered: {name}");
        }
    }

    public Task<JsonElement> DispatchAsync(string name, IServiceProvider services, JsonElement payload, CancellationToken ct) =>
        _handlers[name](services, payload, ct);
}

public sealed class HmiLinkServerBuilder
{
    private readonly IServiceCollection _services;
    private readonly CommandRegistry _registry;

    internal HmiLinkServerBuilder(IServiceCollection services, CommandRegistry registry)
    {
        _services = services;
        _registry = registry;
    }

    public HmiLinkServerBuilder Handle<TCommand, TResult, THandler>()
        where THandler : class, IHmiLinkCommandHandler<TCommand, TResult> =>
        Handle<TCommand, TResult, THandler>(HmiLinkMessageNames.For<TCommand>());

    public HmiLinkServerBuilder OnConnectionChanged<THandler>()
        where THandler : class, IHmiLinkConnectionHandler
    {
        _services.AddScoped<IHmiLinkConnectionHandler, THandler>();
        return this;
    }

    // Explicit name overload for migration / multiple operations using one DTO type.
    public HmiLinkServerBuilder Handle<TCommand, TResult, THandler>(string name)
        where THandler : class, IHmiLinkCommandHandler<TCommand, TResult>
    {
        _services.AddScoped<THandler>();
        _registry.Add<TCommand, TResult, THandler>(name);
        return this;
    }
}
