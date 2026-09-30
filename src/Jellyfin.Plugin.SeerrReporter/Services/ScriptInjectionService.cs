using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.RegularExpressions;
using MediaBrowser.Controller;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SeerrReporter.Services;

/// <summary>
/// Injects the reporter script through File Transformation when available,
/// with an on-disk fallback for servers without that plugin.
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
            if (TryRegisterTransformation())
            {
                return Task.CompletedTask;
            }

            _logger.LogWarning(
                "File Transformation is unavailable; falling back to on-disk script injection. " +
                "Install File Transformation to support read-only jellyfin-web directories");
            ApplyInjection();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not patch jellyfin-web's index.html; install File Transformation for in-memory script injection");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private bool TryRegisterTransformation()
    {
        var assembly = AssemblyLoadContext.All.SelectMany(context => context.Assemblies)
            .FirstOrDefault(candidate => candidate.GetName().Name == "Jellyfin.Plugin.FileTransformation");
        if (assembly is null)
        {
            return false;
        }

        try
        {
            var register = assembly.GetType("Jellyfin.Plugin.FileTransformation.PluginInterface")?
                .GetMethod("RegisterTransformation", BindingFlags.Public | BindingFlags.Static);
            if (register is null)
            {
                throw new MissingMethodException("File Transformation RegisterTransformation was not found");
            }

            var payloadJson = JsonSerializer.Serialize(new
            {
                id = Plugin.Instance!.Id,
                fileNamePattern = @"(?:^|[/\\])index\.html$",
                callbackAssembly = typeof(ScriptInjectionService).Assembly.FullName,
                callbackClass = typeof(ScriptInjectionService).FullName,
                callbackMethod = nameof(TransformIndex)
            });
            // Parse using the plugin's own JObject type to respect its assembly load context.
            var payloadType = register.GetParameters().Single().ParameterType;
            var parse = payloadType.GetMethod("Parse", new[] { typeof(string) })
                ?? throw new MissingMethodException(payloadType.FullName, "Parse");
            var payload = parse.Invoke(null, new object[] { payloadJson });
            register.Invoke(null, new[] { payload });
            _logger.LogInformation("Registered SeerrReporter index.html transformation with File Transformation");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not register SeerrReporter with File Transformation");
            return false;
        }
    }

    /// <summary>Transforms the served index.html using File Transformation's callback payload.</summary>
    public static string TransformIndex(TransformationContents payload) =>
        TransformHtml(payload.Contents, Plugin.Instance?.Configuration.InjectClientScript ?? false);

    /// <summary>Payload deserialized by File Transformation before invoking the callback.</summary>
    public sealed class TransformationContents
    {
        /// <summary>Gets or sets the current HTML contents.</summary>
        public string Contents { get; set; } = string.Empty;
    }

    internal static string TransformHtml(string original, bool shouldInject)
    {
        var stripped = ExistingTagRegex().Replace(original, string.Empty);
        if (!shouldInject)
        {
            return stripped;
        }

        var closingBody = stripped.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        return closingBody < 0 ? original : stripped.Insert(closingBody, ScriptTag);
    }

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
        var updated = TransformHtml(original, shouldInject);

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
