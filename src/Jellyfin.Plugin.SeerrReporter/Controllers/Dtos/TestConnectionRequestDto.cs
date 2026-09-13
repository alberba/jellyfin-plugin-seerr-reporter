namespace Jellyfin.Plugin.SeerrReporter.Controllers.Dtos;

/// <summary>
/// Optional overrides so the dashboard can test values before saving them.
/// </summary>
public class TestConnectionRequestDto
{
    /// <summary>
    /// Gets or sets the Seerr base URL to test. Falls back to the saved configuration.
    /// </summary>
    public string? SeerrUrl { get; set; }

    /// <summary>
    /// Gets or sets the API key to test. Falls back to the saved configuration.
    /// </summary>
    public string? ApiKey { get; set; }
}
