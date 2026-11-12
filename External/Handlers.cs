using HmiLink.Transport;
using HmiLink.Transport.Server;
using Shared;

namespace External;

public sealed record ServerIdentity(string Id);

public sealed class AnimalCommand(IHmiLinkPublisher link, ServerIdentity server)
    : IHmiLinkCommandHandler<Animal, Animal>
{
    public async Task<Animal> HandleAsync(Animal msg, CancellationToken ct)
    {
        Console.WriteLine($"[{server.Id}] HMI -> Animal: {msg.Name}, age {msg.Age}");
        await link.PublishAsync(msg, ct); // Echo as a push event.
        return msg;
    }
}

public sealed class PhoneCommand(IHmiLinkPublisher link, ServerIdentity server)
    : IHmiLinkCommandHandler<Phone, Phone>
{
    public async Task<Phone> HandleAsync(Phone msg, CancellationToken ct)
    {
        Console.WriteLine($"[{server.Id}] HMI -> Phone: {msg.Brand} {msg.Model}");
        await link.PublishAsync(msg, ct);
        return msg;
    }
}

public sealed class ConnectionChanged(ServerIdentity server) : IHmiLinkConnectionHandler
{
    public Task HandleAsync(HmiLinkConnectionEvent e, CancellationToken ct)
    {
        Console.WriteLine($"[{server.Id} CONNECTION] HMI {e.PeerId}: {e.State}");
        return Task.CompletedTask;
    }
}
