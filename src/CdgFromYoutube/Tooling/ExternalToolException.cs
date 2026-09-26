namespace CdgFromYoutube.Tooling;

/// <summary>Reports that an external command failed, together with the end of its error output.</summary>
public sealed class ExternalToolException : KaraokeException
{
    /// <summary>Creates an exception with no message.</summary>
    public ExternalToolException()
    {
    }

    /// <summary>Creates an exception that carries a user facing message.</summary>
    public ExternalToolException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception that carries a user facing message and the cause behind it.</summary>
    public ExternalToolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates the exception for a command that exited with a non zero code.</summary>
    /// <param name="toolName">The name of the tool that failed.</param>
    /// <param name="exitCode">The exit code it reported.</param>
    /// <param name="errorExcerpt">The end of what it wrote to standard error.</param>
    public ExternalToolException(string toolName, int exitCode, string errorExcerpt)
        : base(BuildMessage(toolName, exitCode, errorExcerpt))
    {
        ToolName = toolName;
        ExitCode = exitCode;
    }

    /// <summary>The name of the tool that failed.</summary>
    public string ToolName { get; } = string.Empty;

    /// <summary>The exit code the tool reported.</summary>
    public int ExitCode { get; }

    private static string BuildMessage(string toolName, int exitCode, string errorExcerpt) =>
        string.IsNullOrWhiteSpace(errorExcerpt)
            ? $"{toolName} failed with exit code {exitCode}."
            : $"{toolName} failed with exit code {exitCode}:{Environment.NewLine}{errorExcerpt}";
}
