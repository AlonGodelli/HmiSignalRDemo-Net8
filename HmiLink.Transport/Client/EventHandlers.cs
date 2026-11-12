using System.Text.Json;
using HmiLink.Transport.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace HmiLink.Transport.Client;

internal sealed class EventRegistry
{
    private readonly Dictionary<string, Func<IServiceProvider, string, JsonElement, CancellationToken, Task>> _handlers =
        new(StringComparer.Ordinal);

    public void Add<TEvent, THandler>(string name)
        where THandler : class, IHmiLinkEventHandler<TEvent>
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Event name is required", nameof(name));
        if (!_handlers.TryAdd(name, (sp, serverId, data, ct) =>
            sp.GetRequiredService<THandler>().HandleAsync(serverId, MessageJson.Read<TEvent>(data), ct)))
            throw new InvalidOperationException($"Event already registered: {name}");
    }

    public bool TryGet(string name, out Func<IServiceProvider, string, JsonElement, CancellationToken, Task>? handler) =>
        _handlers.TryGetValue(name, out handler);
}

public sealed class HmiLinkClientBuilder
{
    private readonly IServiceCollection _services;
    private readonly EventRegistry _registry;

    internal HmiLinkClientBuilder(IServiceCollection services, EventRegistry registry)
    {
        _services = services;
        _registry = registry;
    }

    public HmiLinkClientBuilder On<TEvent, THandler>()
        where THandler : class, IHmiLinkEventHandler<TEvent> =>
        On<TEvent, THandler>(HmiLinkMessageNames.For<TEvent>());

    public HmiLinkClientBuilder OnConnectionChanged<THandler>()
        where THandler : class, IHmiLinkConnectionHandler
    {
        _services.AddScoped<IHmiLinkConnectionHandler, THandler>();
        return this;
    }

    // Explicit name overload for migration from older SDK versions.
    public HmiLinkClientBuilder On<TEvent, THandler>(string eventName)
        where THandler : class, IHmiLinkEventHandler<TEvent>
    {
        _services.AddScoped<THandler>();
        _registry.Add<TEvent, THandler>(eventName);
        return this;
    }
}
