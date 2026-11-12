using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HmiLink.Transport.Server;

public static class ServerExtensions
{
    public static IServiceCollection AddHmiLinkServer(
        this IServiceCollection services,
        Action<HmiLinkServerBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        services.AddSignalR();
        services.TryAddSingleton<ConnectionNotifier>();
        var commands = new CommandRegistry();
        configure(new HmiLinkServerBuilder(services, commands));
        services.AddSingleton(commands);
        services.AddSingleton<IHmiLinkPublisher, HmiLinkPublisher>();
        return services;
    }

    public static HubEndpointConventionBuilder MapHmiLinkHub(
        this IEndpointRouteBuilder endpoints, string path = "/hmi-link") =>
        endpoints.MapHub<HmiLinkHub>(path);
}
