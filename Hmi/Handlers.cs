using HmiLink.Transport;
using HmiLink.Transport.Client;
using Shared;

namespace Hmi;

public sealed class AnimalReceived : IHmiLinkEventHandler<Animal>
{
    public Task HandleAsync(string serverId, Animal msg, CancellationToken ct)
    {
        Console.WriteLine($"[HMI] {serverId} -> Animal: {msg.Name}, age {msg.Age}");
        return Task.CompletedTask;
    }
}

public sealed class PhoneReceived : IHmiLinkEventHandler<Phone>
{
    public Task HandleAsync(string serverId, Phone msg, CancellationToken ct)
    {
        Console.WriteLine($"[HMI] {serverId} -> Phone: {msg.Brand} {msg.Model}");
        return Task.CompletedTask;
    }
}

public sealed class ConnectionChanged : IHmiLinkConnectionHandler
{
    public Task HandleAsync(HmiLinkConnectionEvent e, CancellationToken ct)
    {
        Console.WriteLine($"[HMI CONNECTION] {e.PeerId}: {e.State}");
        return Task.CompletedTask;
    }
}
