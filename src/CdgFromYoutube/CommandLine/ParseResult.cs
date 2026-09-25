namespace CdgFromYoutube.CommandLine;

/// <summary>The outcome of reading the command line.</summary>
public sealed class ParseResult
{
    private ParseResult(KaraokeOptions? options, string? error, bool helpRequested)
    {
        Options = options;
        Error = error;
        HelpRequested = helpRequested;
    }

    /// <summary>The options that were read, when reading succeeded.</summary>
    public KaraokeOptions? Options { get; }

    /// <summary>Why reading failed, when it did.</summary>
    public string? Error { get; }

    /// <summary>Whether the command line asked for the help text.</summary>
    public bool HelpRequested { get; }

    /// <summary>Reports a command line that can be run.</summary>
    public static ParseResult Success(KaraokeOptions options) => new(options, null, false);

    /// <summary>Reports a request for the help text.</summary>
    public static ParseResult Help() => new(null, null, true);

    /// <summary>Reports a command line that cannot be used.</summary>
    public static ParseResult Failure(string error) => new(null, error, false);
}
