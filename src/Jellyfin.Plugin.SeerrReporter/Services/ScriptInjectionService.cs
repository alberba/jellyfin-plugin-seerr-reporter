using System.Text.RegularExpressions;
using MediaBrowser.Controller;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SeerrReporter.Services;

/// <summary>
/// Adds (or removes) the reporter script tag in jellyfin-web's index.html.
/// Jellyfin has no supported client-extension point, so patching index.html is
/// how every client-side plugin does this today. The tag is re-applied on every
/// start because a server update overwrites the web directory.
/// </summary>
public partial class ScriptInjectionService : IHostedService
{
    private const string Marker = "SeerrReporter";

    // Relative to /web/, so it keeps working behind a reverse proxy sub-path.
    private const string ScriptTag =
        "<script plugin=\"SeerrReporter\" defer src=\"../SeerrReporter/reporter.js\"></script>";

    private readonly IServerApplicationPaths _applicationPaths;
    private readonly ILogger<ScriptInjectionService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScriptInjectionService"/> class.
    /// </summary>
    /// <param name="applicationPaths">Server paths, used to locate jellyfin-web.</param>
    /// <param name="logger">Logger.</param>
    public ScriptInjectionService(
        IServerApplicationPaths applicationPaths,
        ILogger<ScriptInjectionService> logger)
    {
        _applicationPaths = applicationPaths;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            ApplyInjection();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not patch jellyfin-web's index.html");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [GeneratedRegex("\\s*<script[^>]*plugin=\"SeerrReporter\"[^>]*>\\s*</script>", RegexOptions.IgnoreCase)]
    private static partial Regex ExistingTagRegex();

    private void ApplyInjection()
    {
        var webPath = _applicationPaths.WebPath;
        if (string.IsNullOrEmpty(webPath))
        {
            return;
        }

        var indexPath = Path.Combine(webPath, "index.html");
        if (!File.Exists(indexPath))
        {
            _logger.LogWarning("index.html not found at {Path}; skipping script injection", indexPath);
            return;
        }

        var shouldInject = Plugin.Instance?.Configuration.InjectClientScript ?? false;
        var original = File.ReadAllText(indexPath);
        var stripped = ExistingTagRegex().Replace(original, string.Empty);

        string updated;
        if (shouldInject)
        {
            var closingBody = stripped.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
            if (closingBody < 0)
            {
                _logger.LogWarning("index.html has no </body>; skipping script injection");
                return;
            }

            updated = stripped.Insert(closingBody, ScriptTag);
        }
        else
        {
            updated = stripped;
        }

        if (string.Equals(updated, original, StringComparison.Ordinal))
        {
            return;
        }

        File.WriteAllText(indexPath, updated);
        _logger.LogInformation(
            shouldInject ? "Injected {Marker} script into index.html" : "Removed {Marker} script from index.html",
            Marker);
    }
}
