using System.ComponentModel;
using System.Diagnostics;

namespace CdgFromYoutube.Tooling;

/// <summary>
/// Runs an external command line tool, either capturing its output or handing standard output to a reader.
/// </summary>
public sealed class ExternalTool(string name, string executablePath)
{
    /// <summary>The number of characters of error output that are kept for the failure message.</summary>
    private const int ErrorExcerptLength = 2000;

    /// <summary>The name of the tool, as used in messages.</summary>
    public string Name { get; } = name;

    /// <summary>The full path of the executable.</summary>
    public string ExecutablePath { get; } = executablePath;

    /// <summary>Runs the tool and captures everything it writes.</summary>
    public Task<ExternalToolResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
        RunAsync(arguments, standardOutputConsumer: null, cancellationToken);

    /// <summary>Runs the tool, letting the caller read standard output as it arrives.</summary>
    /// <param name="arguments">The arguments to pass to the tool.</param>
    /// <param name="standardOutputConsumer">
    /// Reads standard output, or <see langword="null"/> to capture it as text instead.
    /// </param>
    /// <param name="cancellationToken">Cancels the run and stops the tool.</param>
    /// <returns>What the tool wrote, once it has finished.</returns>
    /// <remarks>
    /// Standard error is drained while standard output is read, so a tool can never block on a full error
    /// pipe while this process waits for its output.
    /// </remarks>
    public async Task<ExternalToolResult> RunAsync(
        IReadOnlyList<string> arguments,
        Func<Stream, CancellationToken, Task>? standardOutputConsumer,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        ProcessStartInfo startInfo = new(ExecutablePath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new KaraokeException($"Could not start {Name}.");
        using CancellationTokenRegistration registration = cancellationToken.Register(() => Stop(process));

        Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        string standardOutput = string.Empty;
        if (standardOutputConsumer is null)
        {
            standardOutput = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await standardOutputConsumer(process.StandardOutput.BaseStream, cancellationToken).ConfigureAwait(false);
        }

        string standardError = await errorTask.ConfigureAwait(false);
        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (process.ExitCode != 0)
        {
            throw new ExternalToolException(Name, process.ExitCode, Excerpt(standardError));
        }

        return new ExternalToolResult(standardOutput, standardError, process.ExitCode);
    }

    private static void Stop(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited between the check and the kill.
        }
        catch (Win32Exception)
        {
            // The process is already gone.
        }
    }

    private static string Excerpt(string text) =>
        text.Length <= ErrorExcerptLength ? text.Trim() : text[^ErrorExcerptLength..].Trim();
}
