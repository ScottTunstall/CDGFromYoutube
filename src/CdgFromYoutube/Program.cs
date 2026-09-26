using CdgFromYoutube;
using CdgFromYoutube.CommandLine;
using CdgFromYoutube.Pipeline;
using CdgFromYoutube.Tooling;

using CancellationTokenSource cancellation = new();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

ParseResult parsed = CommandLineParser.Parse(args);

// Skipped for a bare --version: that output is meant to be a single machine-readable line, and the
// banner already says the same thing in a friendlier shape.
if (!parsed.VersionRequested)
{
    Console.WriteLine(AppVersion.Banner);
}

if (parsed.HelpRequested)
{
    Console.WriteLine(HelpText.Full);
    return 0;
}

if (parsed.VersionRequested)
{
    Console.WriteLine(AppVersion.Current);
    return 0;
}

ConsoleProgressSink progress = new();

if (parsed.ToolSetup is not null)
{
    return await RunAsync(async () =>
    {
        await ToolLocator.LocateOrDownloadAsync(
            parsed.ToolSetup.FfmpegPath,
            parsed.ToolSetup.YtDlpPath,
            parsed.ToolSetup.JavaScriptRuntimePath,
            allowDownload: true,
            progress,
            cancellation.Token);
        Console.WriteLine("Tools are ready.");
    });
}

if (parsed.Options is null)
{
    Console.Error.WriteLine(parsed.Error);
    Console.Error.WriteLine();
    Console.Error.WriteLine($"Usage: {HelpText.Summary}");
    Console.Error.WriteLine("Run with --help to see every option.");
    return 1;
}

return await RunAsync(async () =>
{
    KaraokeOptions options = parsed.Options;
    bool downloadTools = options.DownloadTools
        || (!ToolLocator.HasRequiredTools(options.FfmpegPath, options.YtDlpPath) && AskToDownloadTools());

    ToolPaths tools = await ToolLocator.LocateOrDownloadAsync(
        options.FfmpegPath,
        options.YtDlpPath,
        options.JavaScriptRuntimePath,
        downloadTools,
        progress,
        cancellation.Token);

    KaraokePipeline pipeline = new(tools, progress);
    await pipeline.RunAsync(options, cancellation.Token);
});

// Only asks when someone is at the keyboard; a script or pipe gets the usual "not found" message instead.
static bool AskToDownloadTools()
{
    if (Console.IsInputRedirected)
    {
        return false;
    }

    Console.Write(
        "yt-dlp and ffmpeg are needed but were not found. Download them now into " +
        $"'{ToolLocator.DefaultToolsDirectory}'? This only needs doing once. [Y/n] ");
    string? answer = Console.ReadLine()?.Trim();
    return answer is not null
        && (answer.Length == 0 || answer.StartsWith('y') || answer.StartsWith('Y'));
}

async Task<int> RunAsync(Func<Task> action)
{
    try
    {
        await action();
        return 0;
    }
    catch (OperationCanceledException)
    {
        Console.Error.WriteLine("Cancelled.");
        return 130;
    }
    catch (KaraokeException exception)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }
}
