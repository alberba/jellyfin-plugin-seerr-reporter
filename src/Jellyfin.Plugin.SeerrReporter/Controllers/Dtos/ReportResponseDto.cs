namespace Jellyfin.Plugin.SeerrReporter.Controllers.Dtos;

/// <summary>
/// Result of a report attempt.
/// </summary>
public class ReportResponseDto
{
    /// <summary>
    /// Gets or sets a value indicating whether the issue was created.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the id of the created Seerr issue.
    /// </summary>
    public int? IssueId { get; set; }

    /// <summary>
    /// Gets or sets a human readable message, used for errors.
    /// </summary>
    public string? Message { get; set; }
}
