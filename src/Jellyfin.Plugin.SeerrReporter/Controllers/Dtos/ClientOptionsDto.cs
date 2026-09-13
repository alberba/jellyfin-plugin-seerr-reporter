namespace Jellyfin.Plugin.SeerrReporter.Controllers.Dtos;

/// <summary>
/// Non-sensitive configuration handed to the browser script.
/// </summary>
public class ClientOptionsDto
{
    /// <summary>
    /// Gets or sets a value indicating whether video reports are offered.
    /// </summary>
    public bool EnableVideoReports { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether audio reports are offered.
    /// </summary>
    public bool EnableAudioReports { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether subtitle reports are offered.
    /// </summary>
    public bool EnableSubtitleReports { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the "Other" type is offered.
    /// </summary>
    public bool EnableOtherReports { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the plugin is configured at all.
    /// </summary>
    public bool Configured { get; set; }
}
