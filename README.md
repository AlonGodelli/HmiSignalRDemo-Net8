# HMI ↔ External: .NET 8 SignalR demo

Four projects in one Visual Studio solution:

- **Hmi** — SignalR client; independently connects to 10 External servers; sends `Animal` and `Phone` to one or all.
- **External** — SignalR Hub server; handles `Animal` and `Phone`, echoes each back as an unsolicited push event, and supports independent push endpoints.
- **HmiLink.Transport** — reusable library (DLL/NuGet package) wrapping SignalR, generic type-based messaging, reconnect, and connection notifications.
- **Shared** — only two business model classes: `Animal` and `Phone`.

## Run (Windows / Visual Studio)

Install the **.NET 8 SDK**. Open `HmiSignalRDemo.sln` and build, then run `Start-Demo.ps1` in PowerShell. This starts **10 External processes** (`localhost:5101` to `5110`) and **one HMI** (`localhost:5000`). Each process logs connection/disconnection events. Stop the processes by closing their windows. Run `Test-Demo.ps1` after startup to exercise both classes in both directions.

Alternatively, for two terminals and just one External instance:

```powershell
dotnet run --project External -- --ServerId=External-01 --urls=http://localhost:5101
dotnet run --project Hmi -- --urls=http://localhost:5000
```

(The HMI will retry connections to the other nine configured endpoints until they become available.)

## Send typed messages

From HMI to one External (response: typed `Animal`):

```powershell
Invoke-RestMethod http://localhost:5000/animal/External-01 -Method Post -ContentType 'application/json' -Body '{"name":"Dog","age":3}'
```

From HMI to **all ten** (result per server):

```powershell
Invoke-RestMethod http://localhost:5000/phone/all -Method Post -ContentType 'application/json' -Body '{"brand":"Apple","model":"iPhone"}'
```

From an External server to HMI **without HMI polling**:

```powershell
Invoke-RestMethod http://localhost:5101/push/animal -Method Post -ContentType 'application/json' -Body '{"name":"Cat","age":2}'
Invoke-RestMethod http://localhost:5102/push/phone -Method Post -ContentType 'application/json' -Body '{"brand":"Samsung","model":"Galaxy"}'
```

You can also use `POST /animal/all`, `/animal/{id}`, `/phone/{id}`, `/phone/all` from any HTTP client. Each received message and each connection/disconnection is printed in its destination terminal. HMI requests also cause External to echo the message as a push event.

## Inside the applications

**HMI initialization:**

```csharp
builder.Services.AddHmiLinkClient(builder.Configuration.GetSection("HmiLink"), events => events
    .On<Animal, AnimalReceived>().On<Phone, PhoneReceived>()
    .OnConnectionChanged<ConnectionChanged>());
```

**External initialization:**

```csharp
builder.Services.AddHmiLinkServer(commands => commands
    .Handle<Animal, Animal, AnimalCommand>()
    .Handle<Phone, Phone, PhoneCommand>()
    .OnConnectionChanged<ConnectionChanged>());
app.MapHmiLinkHub();
```

The compiled library will be at `HmiLink.Transport/bin/Debug/net8.0/HmiLink.Transport.dll` after building. For packaging: `dotnet pack HmiLink.Transport -c Release`.

## Notes

This is an intentionally small **local development demo**, with plaintext HTTP and no authentication. Use HTTPS, client authentication/authorization, and approved message origin checks for production. It does not queue missed events or automatically retry commands (to avoid duplicate actions). After reconnect, request fresh state if it matters. No periodic HTTP polling is used; pushed messages are delivered only to connected clients.
