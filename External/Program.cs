using External;
using HmiLink.Transport.Server;
using Shared;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(new ServerIdentity(builder.Configuration["ServerId"] ?? "External-01"));
builder.Services.AddHmiLinkServer(commands => commands
    .Handle<Animal, Animal, AnimalCommand>()
    .Handle<Phone, Phone, PhoneCommand>()
    .OnConnectionChanged<ConnectionChanged>());

var app = builder.Build();
app.MapHmiLinkHub();

// External -> HMI: push messages whenever business data changes (no polling).
app.MapPost("/push/animal", async (Animal msg, IHmiLinkPublisher link, CancellationToken ct) =>
{
    await link.PublishAsync(msg, ct);
    return Results.Ok();
});
app.MapPost("/push/phone", async (Phone msg, IHmiLinkPublisher link, CancellationToken ct) =>
{
    await link.PublishAsync(msg, ct);
    return Results.Ok();
});

app.Run();
