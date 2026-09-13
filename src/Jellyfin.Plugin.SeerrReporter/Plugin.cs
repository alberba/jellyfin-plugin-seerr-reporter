using Jellyfin.Plugin.SeerrReporter.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.SeerrReporter;

/// <summary>
/// Bridges Jellyfin playback issue reports to Seerr / Overseerr issues.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Server application paths.</param>
    /// <param name="xmlSerializer">Xml serializer used for the configuration file.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => "Seerr Reporter";

    /// <inheritdoc />
    public override string Description =>
        "Lets users report playback issues (video, audio, subtitles) straight to Seerr / Overseerr.";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("6f3c1f4e-0d6b-4d2a-9a2b-1f5f9c7a8e21");

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        yield return new PluginPageInfo
        {
            Name = Name,
            EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.html"
        };
    }
}
