using CdgFromYoutube.CommandLine;
using CdgFromYoutube.Imaging;

namespace CdgFromYoutube.Tests;

public sealed class CommandLineParserTests
{
    private const string VideoUrl = "https://www.youtube.com/watch?v=abcdefghijk";

    [Fact]
    public void HelpIsRecognised() => Assert.True(CommandLineParser.Parse(["--help"]).HelpRequested);

    [Fact]
    public void ACommandLineWithoutAUrlIsRejected()
    {
        ParseResult result = CommandLineParser.Parse([]);

        Assert.Null(result.Options);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void AnOptionThatIsNotKnownIsReported()
    {
        ParseResult result = CommandLineParser.Parse(["--nonsense", VideoUrl]);

        Assert.Null(result.Options);
        Assert.Contains("--nonsense", result.Error ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void TwoUrlsAreRejected()
    {
        ParseResult result = CommandLineParser.Parse([VideoUrl, VideoUrl]);

        Assert.Null(result.Options);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void OptionsAreReadInAnyOrder()
    {
        ParseResult result = CommandLineParser.Parse(
        [
            "--dither",
            "-o", "out",
            "--fps", "10",
            VideoUrl,
            "--mp3-bitrate", "128",
            "--keep-temp",
        ]);

        KaraokeOptions options = Assert.IsType<KaraokeOptions>(result.Options);
        Assert.True(options.UseDither);
        Assert.True(options.KeepTemporaryFiles);
        Assert.Equal(10, options.VideoFrameRate);
        Assert.Equal(128, options.Mp3BitRateKbps);
        Assert.Equal(VideoUrl, options.VideoUrl.AbsoluteUri);
        Assert.EndsWith("out", options.OutputDirectory, StringComparison.Ordinal);
    }

    [Fact]
    public void AnOptionWithoutAValueIsRejected()
    {
        ParseResult result = CommandLineParser.Parse([VideoUrl, "--output"]);

        Assert.Null(result.Options);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void TheJavaScriptRuntimePathIsRead()
    {
        ParseResult result = CommandLineParser.Parse(["--js-runtime", @"C:\tools\deno.exe", VideoUrl]);

        KaraokeOptions options = Assert.IsType<KaraokeOptions>(result.Options);
        Assert.Equal(@"C:\tools\deno.exe", options.JavaScriptRuntimePath);
    }

    [Fact]
    public void CropMarginsAreReadAsLeftTopRightBottom()
    {
        ParseResult result = CommandLineParser.Parse([VideoUrl, "--crop", "15,7,14,6"]);

        KaraokeOptions options = Assert.IsType<KaraokeOptions>(result.Options);
        Assert.Equal(new CropMargins(15, 7, 14, 6), options.Crop);
        Assert.False(options.AutoCrop);
    }

    [Fact]
    public void CropAutoAsksForTheLyricsToBeFound()
    {
        ParseResult result = CommandLineParser.Parse([VideoUrl, "--crop", "auto"]);

        KaraokeOptions options = Assert.IsType<KaraokeOptions>(result.Options);
        Assert.True(options.AutoCrop);
        Assert.True(options.Crop.IsNone);
    }

    [Theory]
    [InlineData("--crop", "10,10,10")]
    [InlineData("--crop", "10,ten,10,10")]
    [InlineData("--crop", "-1,0,0,0")]
    [InlineData("--crop", "50,0,45,0")]
    [InlineData("--fps", "0")]
    [InlineData("--fps", "61")]
    [InlineData("--fps", "quickly")]
    [InlineData("--mp3-bitrate", "999")]
    [InlineData("--mp3-bitrate", "zero")]
    [InlineData("--mp3-sample-rate", "12345")]
    [InlineData("--max-source-height", "0")]
    public void ValuesOutsideTheAllowedRangeAreReported(string option, string value)
    {
        ParseResult result = CommandLineParser.Parse([option, value, VideoUrl]);

        Assert.Null(result.Options);
        Assert.NotNull(result.Error);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://example.com/video")]
    public void AddressesThatAreNotHttpAreReported(string url)
    {
        ParseResult result = CommandLineParser.Parse([url]);

        Assert.Null(result.Options);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void TheDefaultsMatchTheDocumentedBehaviour()
    {
        ParseResult result = CommandLineParser.Parse([VideoUrl]);

        KaraokeOptions options = Assert.IsType<KaraokeOptions>(result.Options);
        Assert.Equal(KaraokeOptions.DefaultVideoFrameRate, options.VideoFrameRate);
        Assert.Equal(KaraokeOptions.DefaultMp3BitRateKbps, options.Mp3BitRateKbps);
        Assert.Null(options.Mp3SampleRate);
        Assert.False(options.UseDither);
        Assert.False(options.UseSafeArea);
        Assert.True(options.Crop.IsNone);
        Assert.False(options.AutoCrop);
        Assert.False(options.DownloadTools);
        Assert.Null(options.JavaScriptRuntimePath);
    }
}
