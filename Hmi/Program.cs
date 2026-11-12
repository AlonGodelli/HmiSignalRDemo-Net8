using Hmi;
using HmiLink.Transport.Client;
using Shared;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHmiLinkClient(builder.Configuration.GetSection("HmiLink"), events => events
    .On<Animal, AnimalReceived>()
    .On<Phone, PhoneReceived>()
    .OnConnectionChanged<ConnectionChanged>());

var app = builder.Build();

// HMI -> one External or all Externals. Replies are typed classes too.
app.MapPost("/animal/all", (Animal msg, IHmiLinkClient link, CancellationToken ct) =>
    link.SendToAllAsync<Animal, Animal>(msg, ct));
app.MapPost("/animal/{id}", (string id, Animal msg, IHmiLinkClient link, CancellationToken ct) =>
    link.SendAsync<Animal, Animal>(id, msg, ct));
app.MapPost("/phone/all", (Phone msg, IHmiLinkClient link, CancellationToken ct) =>
    link.SendToAllAsync<Phone, Phone>(msg, ct));
app.MapPost("/phone/{id}", (string id, Phone msg, IHmiLinkClient link, CancellationToken ct) =>
    link.SendAsync<Phone, Phone>(id, msg, ct));
app.MapGet("/connected/{id}", (string id, IHmiLinkClient link) => link.IsConnected(id));

app.Run();
