using System.ComponentModel.DataAnnotations;

namespace Jellyfin.Plugin.SeerrReporter.Controllers.Dtos;

/// <summary>
/// Body of POST /Plugins/SeerrReporter/Report.
/// </summary>
public class ReportRequestDto
{
    /// <summary>
    /// Gets or sets the Jellyfin item id the user is reporting.
    /// </summary>
    [Required]
    public Guid ItemId { get; set; }

    /// <summary>
    /// Gets or sets the Seerr issue type: 1 video, 2 audio, 3 subtitles, 4 other.
    /// </summary>
    [Range(1, 4)]
    public int IssueType { get; set; } = 4;

    /// <summary>
    /// Gets or sets the optional free-text note written by the user.
    /// </summary>
    public string? Comment { get; set; }
}
