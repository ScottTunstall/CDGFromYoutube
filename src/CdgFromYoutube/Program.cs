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
if (parsed.HelpRequested)
{
    Console.WriteLine(HelpText.Full);
    return 0;
}

if (parsed.Options is null)
{
    Console.Error.WriteLine(parsed.Error);
    Console.Error.WriteLine();
    Console.Error.WriteLine($"Usage: {HelpText.Summary}");
    Console.Error.WriteLine("Run with --help to see every option.");
    return 1;
}

ConsoleProgressSink progress = new();

try
{
    KaraokeOptions options = parsed.Options;
    ToolPaths tools = await ToolLocator.LocateOrDownloadAsync(
        options.FfmpegPath,
        options.YtDlpPath,
        options.JavaScriptRuntimePath,
        options.DownloadTools,
        progress,
        cancellation.Token);

    KaraokePipeline pipeline = new(tools, progress);
    await pipeline.RunAsync(options, cancellation.Token);
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
