namespace CdgFromYoutube;

/// <summary>
/// An error that describes something the user can act on, such as a missing tool or an unusable video.
/// </summary>
/// <remarks>
/// The program reports these messages without a stack trace.
/// </remarks>
public class KaraokeException : Exception
{
    /// <summary>Creates an exception that carries a user facing message.</summary>
    public KaraokeException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception that carries a user facing message and the cause behind it.</summary>
    public KaraokeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
