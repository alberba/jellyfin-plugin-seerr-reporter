using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.SeerrReporter.Configuration;

/// <summary>
/// Plugin configuration, edited from Dashboard -> Plugins -> Seerr Reporter.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the base URL of the Seerr / Overseerr / Jellyseerr instance,
    /// e.g. http://seerr:5055 when both containers share the same Docker network.
    /// </summary>
    public string SeerrUrl { get; set; } = "http://seerr:5055";

    /// <summary>
    /// Gets or sets the Seerr API key. Never leaves the server: the browser only
    /// ever talks to the Jellyfin plugin endpoint.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether video issues can be reported.
    /// </summary>
    public bool EnableVideoReports { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether audio issues can be reported.
    /// </summary>
    public bool EnableAudioReports { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether subtitle issues can be reported.
    /// </summary>
    public bool EnableSubtitleReports { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the generic "Other" issue type is offered.
    /// </summary>
    public bool EnableOtherReports { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the reporter script is injected into
    /// jellyfin-web's index.html on startup. Requires a writable web directory.
    /// </summary>
    public bool InjectClientScript { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the reporting user's Jellyfin username
    /// is appended to the issue message.
    /// </summary>
    public bool IncludeUsernameInMessage { get; set; } = true;
}
