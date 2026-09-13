using Jellyfin.Plugin.SeerrReporter.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.SeerrReporter;

/// <summary>
/// Registers the plugin services with the host container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddScoped<SeerrClient>();
        serviceCollection.AddHostedService<ScriptInjectionService>();
    }
}
