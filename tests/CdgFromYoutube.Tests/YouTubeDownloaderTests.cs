using CdgFromYoutube.Media;
using CdgFromYoutube.Tooling;

namespace CdgFromYoutube.Tests;

public sealed class YouTubeDownloaderTests
{
    private const string VideoUrl = "https://www.example.com/watch?v=1";
    private const string RuntimePath = @"C:\tools\deno.exe";

    [Fact]
    public void TheJavaScriptRuntimeIsPassedOnToYtDlp()
    {
        IReadOnlyList<string> arguments = BuildArguments(runtimePath: RuntimePath, maximumSourceHeight: null);

        Assert.Contains("--js-runtimes", arguments);
        Assert.Contains($"deno:{RuntimePath}", arguments);
        Assert.Equal(VideoUrl, arguments[^1]);
    }

    [Fact]
    public void NoRuntimeOptionIsPassedWhenThereIsNoRuntime()
    {
        IReadOnlyList<string> arguments = BuildArguments(runtimePath: null, maximumSourceHeight: null);

        Assert.DoesNotContain("--js-runtimes", arguments);
    }

    [Fact]
    public void AHeightLimitPrefersFormatsWithinItWithoutRulingOutTheRest()
    {
        IReadOnlyList<string> arguments = BuildArguments(runtimePath: null, maximumSourceHeight: 360);

        int sortIndex = IndexOf(arguments, "--format-sort");
        Assert.Equal("res:360", arguments[sortIndex + 1]);
        Assert.Equal("bv*+ba/b", arguments[IndexOf(arguments, "--format") + 1]);
    }

    [Fact]
    public void NoFormatSortIsPassedWithoutAHeightLimit()
    {
        IReadOnlyList<string> arguments = BuildArguments(runtimePath: null, maximumSourceHeight: null);

        Assert.DoesNotContain("--format-sort", arguments);
        Assert.Equal("bv*+ba/b", arguments[IndexOf(arguments, "--format") + 1]);
    }

    [Fact]
    public void TheTitleAndTheFilePathAreBothReported()
    {
        IReadOnlyList<string> arguments = BuildArguments(runtimePath: null, maximumSourceHeight: null);

        Assert.Contains("after_move:%(title)s", arguments);
        Assert.Contains("after_move:%(filepath)s", arguments);
    }

    [Fact]
    public void DownloadsGoIntoTheGivenFolder()
    {
        IReadOnlyList<string> arguments = BuildArguments(runtimePath: null, maximumSourceHeight: null);

        Assert.Contains(Path.Combine("downloads", "source.%(ext)s"), arguments);
    }

    private static IReadOnlyList<string> BuildArguments(string? runtimePath, int? maximumSourceHeight)
    {
        YouTubeDownloader downloader = new(
            new ExternalTool("yt-dlp", "yt-dlp.exe"),
            new SilentProgressSink(),
            runtimePath);

        return downloader.BuildArguments(new Uri(VideoUrl), "downloads", maximumSourceHeight);
    }

    private static int IndexOf(IReadOnlyList<string> arguments, string option)
    {
        int index = arguments.ToList().IndexOf(option);
        Assert.True(index >= 0, $"{option} was not passed.");
        return index;
    }

    /// <summary>A sink that throws the progress away, for the tests that only care about what is built.</summary>
    private sealed class SilentProgressSink : IProgressSink
    {
        public void Stage(string message)
        {
        }

        public void Detail(string message)
        {
        }

        public void Warning(string message)
        {
        }

        public void Progress(double fraction, string message)
        {
        }

        public void FinishProgress()
        {
        }

        public void Completed(string message)
        {
        }
    }
}
