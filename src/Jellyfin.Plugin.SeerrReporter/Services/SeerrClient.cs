using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Jellyfin.Plugin.SeerrReporter.Configuration;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SeerrReporter.Services;

/// <summary>
/// Thin wrapper over the Seerr / Overseerr v1 API.
/// </summary>
public class SeerrClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SeerrClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeerrClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">Factory for the outbound http client.</param>
    /// <param name="logger">Logger.</param>
    public SeerrClient(IHttpClientFactory httpClientFactory, ILogger<SeerrClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private static PluginConfiguration Config =>
        Plugin.Instance?.Configuration ?? throw new SeerrException("Plugin is not initialised.");

    /// <summary>
    /// Checks that the URL is reachable and the API key is accepted.
    /// </summary>
    /// <param name="baseUrl">Optional URL override.</param>
    /// <param name="apiKey">Optional API key override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The Seerr version string.</returns>
    public async Task<string> TestConnectionAsync(
        string? baseUrl,
        string? apiKey,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient(baseUrl, apiKey);
        using var response = await client.GetAsync("api/v1/settings/about", cancellationToken)
            .ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        using var document = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
        return document.RootElement.TryGetProperty("version", out var version)
            ? version.GetString() ?? "unknown"
            : "unknown";
    }

    /// <summary>
    /// Resolves the internal Seerr media id for a TMDB id.
    /// </summary>
    /// <param name="isTv">True for series/episodes, false for movies.</param>
    /// <param name="tmdbId">The TMDB id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The Seerr media id.</returns>
    public async Task<int> GetMediaIdAsync(bool isTv, int tmdbId, CancellationToken cancellationToken)
    {
        using var client = CreateClient(null, null);
        var path = string.Create(
            CultureInfo.InvariantCulture,
            $"api/v1/{(isTv ? "tv" : "movie")}/{tmdbId}");

        using var response = await client.GetAsync(path, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new SeerrException("Seerr does not know this title (TMDB id not found).");
        }

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        using var document = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);

        if (!document.RootElement.TryGetProperty("mediaInfo", out var mediaInfo)
            || mediaInfo.ValueKind != JsonValueKind.Object
            || !mediaInfo.TryGetProperty("id", out var id))
        {
            // Seerr only creates mediaInfo once the title has been requested or scanned.
            throw new SeerrException("This title is not tracked by Seerr yet, so no issue can be opened for it.");
        }

        return id.GetInt32();
    }

    /// <summary>
    /// Creates an issue in Seerr.
    /// </summary>
    /// <param name="mediaId">Seerr internal media id.</param>
    /// <param name="issueType">1 video, 2 audio, 3 subtitles, 4 other.</param>
    /// <param name="message">Issue body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created issue id.</returns>
    public async Task<int> CreateIssueAsync(
        int mediaId,
        int issueType,
        string message,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient(null, null);
        var payload = new
        {
            mediaId,
            issueType,
            message
        };

        using var response = await client
            .PostAsJsonAsync("api/v1/issue", payload, cancellationToken)
            .ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        using var document = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
        return document.RootElement.TryGetProperty("id", out var id) ? id.GetInt32() : 0;
    }

    private HttpClient CreateClient(string? baseUrl, string? apiKey)
    {
        var url = (baseUrl ?? Config.SeerrUrl ?? string.Empty).Trim().TrimEnd('/');
        var key = (apiKey ?? Config.ApiKey ?? string.Empty).Trim();

        if (url.Length == 0)
        {
            throw new SeerrException("Seerr URL is not configured.");
        }

        if (key.Length == 0)
        {
            throw new SeerrException("Seerr API key is not configured.");
        }

        if (!Uri.TryCreate(url + "/", UriKind.Absolute, out var baseUri))
        {
            throw new SeerrException($"'{url}' is not a valid absolute URL.");
        }

        var client = _httpClientFactory.CreateClient(NamedClient.Default);
        client.BaseAddress = baseUri;
        client.Timeout = TimeSpan.FromSeconds(20);
        client.DefaultRequestHeaders.Remove("X-Api-Key");
        client.DefaultRequestHeaders.Add("X-Api-Key", key);
        return client;
    }

    private static async Task<JsonDocument> ReadJsonAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogError(
            "Seerr returned {StatusCode} for {Uri}: {Body}",
            (int)response.StatusCode,
            response.RequestMessage?.RequestUri,
            body);

        throw new SeerrException(response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                "Seerr rejected the API key.",
            HttpStatusCode.NotFound =>
                "Seerr endpoint not found. Check the URL (it must point at the Seerr root, not /api).",
            _ => $"Seerr returned HTTP {(int)response.StatusCode}."
        });
    }
}
