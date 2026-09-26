namespace CdgFromYoutube.CommandLine;

/// <summary>The outcome of reading the command line.</summary>
public sealed class ParseResult
{
    private ParseResult(
        KaraokeOptions? options,
        string? error,
        bool helpRequested,
        bool versionRequested,
        ToolSetupOptions? toolSetup)
    {
        Options = options;
        Error = error;
        HelpRequested = helpRequested;
        VersionRequested = versionRequested;
        ToolSetup = toolSetup;
    }

    /// <summary>The options that were read, when reading succeeded.</summary>
    public KaraokeOptions? Options { get; }

    /// <summary>Why reading failed, when it did.</summary>
    public string? Error { get; }

    /// <summary>Whether the command line asked for the help text.</summary>
    public bool HelpRequested { get; }

    /// <summary>Whether the command line asked for the program's version.</summary>
    public bool VersionRequested { get; }

    /// <summary>Set when the command line only asked to fetch tools, with no video to convert.</summary>
    public ToolSetupOptions? ToolSetup { get; }

    /// <summary>Reports a command line that can be run.</summary>
    public static ParseResult Success(KaraokeOptions options) => new(options, null, false, false, null);

    /// <summary>Reports a request for the help text.</summary>
    public static ParseResult Help() => new(null, null, true, false, null);

    /// <summary>Reports a request for the program's version.</summary>
    public static ParseResult Version() => new(null, null, false, true, null);

    /// <summary>Reports a command line that cannot be used.</summary>
    public static ParseResult Failure(string error) => new(null, error, false, false, null);

    /// <summary>Reports a command line that only asked to fetch tools.</summary>
    public static ParseResult ForToolSetup(ToolSetupOptions options) => new(null, null, false, false, options);
}
