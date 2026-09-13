using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.SeerrReporter.Controllers;

/// <summary>
/// Serves the browser script that jellyfin-web loads via the injected script tag.
/// Anonymous on purpose: a plain script tag carries no Jellyfin token.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("SeerrReporter")]
public class ClientScriptController : ControllerBase
{
    /// <summary>
    /// Returns the reporter script.
    /// </summary>
    /// <returns>The javascript file.</returns>
    [HttpGet("reporter.js")]
    public ActionResult GetReporterScript()
    {
        var stream = typeof(Plugin).Assembly
            .GetManifestResourceStream("Jellyfin.Plugin.SeerrReporter.Web.reporter.js");

        if (stream is null)
        {
            return NotFound();
        }

        return File(stream, "application/javascript; charset=utf-8");
    }
}
