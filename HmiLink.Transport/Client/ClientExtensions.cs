using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace HmiLink.Transport.Client;

public static class ClientExtensions
{
    public static IServiceCollection AddHmiLinkClient(
        this IServiceCollection services,
        IConfigurationSection section,
        Action<HmiLinkClientBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        services.Configure<HmiLinkClientOptions>(section);
        var events = new EventRegistry();
        configure(new HmiLinkClientBuilder(services, events));
        services.AddSingleton(events);
        services.TryAddSingleton<ConnectionNotifier>();
        services.TryAddSingleton<IHmiLinkTokenProvider, EmptyTokenProvider>();

        // One instance for both the hosted lifetime and application command API.
        services.AddSingleton<HmiLinkConnectionManager>();
        services.AddSingleton<IHmiLinkClient>(sp => sp.GetRequiredService<HmiLinkConnectionManager>());
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<HmiLinkConnectionManager>());
        return services;
    }
}
