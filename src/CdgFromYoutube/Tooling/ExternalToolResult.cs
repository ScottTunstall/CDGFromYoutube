namespace CdgFromYoutube.Tooling;

/// <summary>The text an external command wrote and the code it exited with.</summary>
/// <param name="StandardOutput">Everything the command wrote to standard output.</param>
/// <param name="StandardError">Everything the command wrote to standard error.</param>
/// <param name="ExitCode">The exit code of the command.</param>
public sealed record ExternalToolResult(string StandardOutput, string StandardError, int ExitCode);
