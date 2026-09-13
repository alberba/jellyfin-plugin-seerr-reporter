using System.Globalization;
using System.Security.Claims;
using System.Text;
using Jellyfin.Plugin.SeerrReporter.Configuration;
using Jellyfin.Plugin.SeerrReporter.Controllers.Dtos;
using Jellyfin.Plugin.SeerrReporter.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SeerrReporter.Controllers;

/// <summary>
/// Receives issue reports from jellyfin-web and forwards them to Seerr.
/// </summary>
[ApiController]
[Authorize(Policy = "DefaultAuthorization")]
[Route("Plugins/SeerrReporter")]
[Produces("application/json")]
public class ReportController : ControllerBase
{
    private const string UserIdClaim = "Jellyfin-UserId";

    private readonly ILibraryManager _libraryManager;
    private readonly IUserManager _userManager;
    private readonly SeerrClient _seerrClient;
    private readonly ILogger<ReportController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportController"/> class.
    /// </summary>
    /// <param name="libraryManager">Library manager.</param>
    /// <param name="userManager">User manager.</param>
    /// <param name="seerrClient">Seerr API client.</param>
    /// <param name="logger">Logger.</param>
    public ReportController(
        ILibraryManager libraryManager,
        IUserManager userManager,
        SeerrClient seerrClient,
        ILogger<ReportController> logger)
    {
        _libraryManager = libraryManager;
        _userManager = userManager;
        _seerrClient = seerrClient;
        _logger = logger;
    }

    private static PluginConfiguration Config =>
        Plugin.Instance?.Configuration ?? new PluginConfiguration();

    /// <summary>
    /// Returns the non-sensitive options the browser script needs.
    /// </summary>
    /// <returns>Client options.</returns>
    [HttpGet("ClientOptions")]
    public ActionResult<ClientOptionsDto> GetClientOptions()
    {
        var config = Config;
        return new ClientOptionsDto
        {
            EnableVideoReports = config.EnableVideoReports,
            EnableAudioReports = config.EnableAudioReports,
            EnableSubtitleReports = config.EnableSubtitleReports,
            EnableOtherReports = config.EnableOtherReports,
            Configured = !string.IsNullOrWhiteSpace(config.SeerrUrl)
                         && !string.IsNullOrWhiteSpace(config.ApiKey)
        };
    }

    /// <summary>
    /// Creates a Seerr issue for a Jellyfin item.
    /// </summary>
    /// <param name="request">The report.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created issue id.</returns>
    [HttpPost("Report")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReportResponseDto>> Report(
        [FromBody] ReportRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!IsTypeEnabled(request.IssueType))
        {
            return BadRequest(Fail("That issue type is disabled by the administrator."));
        }

        var item = _libraryManager.GetItemById(request.ItemId);
        if (item is null)
        {
            return BadRequest(Fail("Item not found in the Jellyfin library."));
        }

        if (!ResolveTmdbId(item, out var tmdbId, out var isTv))
        {
            return BadRequest(Fail(
                "This item has no TMDB id in its metadata, so it cannot be matched in Seerr."));
        }

        try
        {
            var mediaId = await _seerrClient.GetMediaIdAsync(isTv, tmdbId, cancellationToken)
                .ConfigureAwait(false);

            var message = BuildMessage(item, request.Comment);

            var issueId = await _seerrClient
                .CreateIssueAsync(mediaId, request.IssueType, message, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Created Seerr issue {IssueId} (type {IssueType}) for item {ItemName}",
                issueId,
                request.IssueType,
                item.Name);

            return new ReportResponseDto { Success = true, IssueId = issueId };
        }
        catch (SeerrException ex)
        {
            return BadRequest(Fail(ex.Message));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "Could not reach Seerr");
            return BadRequest(Fail("Could not reach Seerr. Check the URL and that both containers share a network."));
        }
    }

    /// <summary>
    /// Validates the configured (or supplied) Seerr URL and API key.
    /// </summary>
    /// <param name="request">Optional overrides.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Connection result.</returns>
    [HttpPost("TestConnection")]
    [Authorize(Policy = "RequiresElevation")]
    public async Task<ActionResult<ReportResponseDto>> TestConnection(
        [FromBody] TestConnectionRequestDto? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var version = await _seerrClient
                .TestConnectionAsync(request?.SeerrUrl, request?.ApiKey, cancellationToken)
                .ConfigureAwait(false);

            return new ReportResponseDto
            {
                Success = true,
                Message = $"Connected to Seerr {version}."
            };
        }
        catch (SeerrException ex)
        {
            return BadRequest(Fail(ex.Message));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "Could not reach Seerr");
            return BadRequest(Fail("Could not reach Seerr at that URL."));
        }
    }

    private static ReportResponseDto Fail(string message) =>
        new() { Success = false, Message = message };

    private static bool IsTypeEnabled(int issueType)
    {
        var config = Config;
        return issueType switch
        {
            1 => config.EnableVideoReports,
            2 => config.EnableAudioReports,
            3 => config.EnableSubtitleReports,
            4 => config.EnableOtherReports,
            _ => false
        };
    }

    /// <summary>
    /// Seerr indexes everything by TMDB id, including series. Episodes and seasons
    /// are reported against their series.
    /// </summary>
    private static bool ResolveTmdbId(BaseItem item, out int tmdbId, out bool isTv)
    {
        var target = item switch
        {
            Episode episode => (BaseItem?)episode.Series ?? episode,
            Season season => (BaseItem?)season.Series ?? season,
            _ => item
        };

        isTv = target is Series or Season or Episode;

        var raw = target.GetProviderId(MediaBrowser.Model.Entities.MetadataProvider.Tmdb);
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out tmdbId)
               && tmdbId > 0;
    }

    private string BuildMessage(BaseItem item, string? comment)
    {
        var builder = new StringBuilder();

        if (item is Episode episode)
        {
            builder.Append(CultureInfo.InvariantCulture, $"{episode.SeriesName} ");
            if (episode.ParentIndexNumber.HasValue && episode.IndexNumber.HasValue)
            {
                builder.Append(CultureInfo.InvariantCulture, $"S{episode.ParentIndexNumber:00}E{episode.IndexNumber:00} ");
            }

            builder.AppendLine(CultureInfo.InvariantCulture, $"- {episode.Name}");
        }
        else
        {
            builder.AppendLine(item.Name);
        }

        if (!string.IsNullOrWhiteSpace(comment))
        {
            builder.AppendLine();
            builder.AppendLine(comment.Trim());
        }

        if (Config.IncludeUsernameInMessage)
        {
            var userName = GetUserName();
            if (!string.IsNullOrEmpty(userName))
            {
                builder.AppendLine();
                builder.Append(CultureInfo.InvariantCulture, $"_Reported from Jellyfin by {userName}._");
            }
        }

        return builder.ToString().Trim();
    }

    private string? GetUserName()
    {
        var raw = User.FindFirstValue(UserIdClaim);
        if (Guid.TryParse(raw, out var userId))
        {
            var user = _userManager.GetUserById(userId);
            if (user is not null)
            {
                return user.Username;
            }
        }

        return User.Identity?.Name;
    }
}
