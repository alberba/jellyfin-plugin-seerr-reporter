namespace Jellyfin.Plugin.SeerrReporter.Services;

/// <summary>
/// Raised when Seerr answers with something we cannot use.
/// </summary>
public class SeerrException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SeerrException"/> class.
    /// </summary>
    /// <param name="message">Message shown to the user.</param>
    public SeerrException(string message)
        : base(message)
    {
    }
}
