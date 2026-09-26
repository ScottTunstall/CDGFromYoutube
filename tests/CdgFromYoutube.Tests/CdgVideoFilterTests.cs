using CdgFromYoutube.Cdg;
using CdgFromYoutube.Imaging;
using CdgFromYoutube.Media;

namespace CdgFromYoutube.Tests;

public sealed class CdgVideoFilterTests
{
    [Theory]
    [InlineData(1920, 1080, 300, 168)]
    [InlineData(1280, 720, 300, 168)]
    [InlineData(640, 480, 288, 216)]
    [InlineData(300, 216, 300, 216)]
    [InlineData(160, 120, 288, 216)]
    public void FitInsidePreservesTheShapeOfTheVideo(int sourceWidth, int sourceHeight, int expectedWidth, int expectedHeight)
    {
        FrameSize fitted = AspectFit.FitInside(
            new FrameSize(sourceWidth, sourceHeight),
            new FrameSize(CdgFormat.Width, CdgFormat.Height));

        Assert.Equal(new FrameSize(expectedWidth, expectedHeight), fitted);
    }

    [Theory]
    [InlineData(1920, 1080, true)]
    [InlineData(160, 120, false)]
    public void IsDownScaleReportsWhetherPixelsAreThrownAway(int width, int height, bool expected) =>
        Assert.Equal(
            expected,
            AspectFit.IsDownScale(new FrameSize(width, height), new FrameSize(CdgFormat.Width, CdgFormat.Height)));

    [Fact]
    public void TheFilterChainScalesIntoTheRasterAndPadsItOutToTheFullSize()
    {
        string filter = CdgVideoFilter.Build(framesPerSecond: 15, useSafeArea: false);

        Assert.Contains("fps=15", filter, StringComparison.Ordinal);
        Assert.Contains($"scale={CdgFormat.Width}:{CdgFormat.Height}:force_original_aspect_ratio=decrease", filter, StringComparison.Ordinal);
        Assert.Contains($"pad={CdgFormat.Width}:{CdgFormat.Height}:(ow-iw)/2:(oh-ih)/2:color=black", filter, StringComparison.Ordinal);
        Assert.Contains("eq=saturation=1.5", filter, StringComparison.Ordinal);
        Assert.Contains("format=rgb24", filter, StringComparison.Ordinal);
    }

    [Fact]
    public void ColorIsSpreadToEveryPixelBeforeTheFrameIsScaled()
    {
        // Scaled in the video's own layout, the color would only be kept at half the width and height.
        string filter = CdgVideoFilter.Build(framesPerSecond: 15, useSafeArea: false);

        int fullColor = filter.IndexOf("format=yuv444p", StringComparison.Ordinal);
        Assert.InRange(fullColor, 0, filter.IndexOf("scale=", StringComparison.Ordinal));
    }

    [Fact]
    public void SharpeningCanBeLeftOut()
    {
        Assert.Contains("unsharp=", CdgVideoFilter.Build(framesPerSecond: 15, useSafeArea: false), StringComparison.Ordinal);
        Assert.DoesNotContain(
            "unsharp=",
            CdgVideoFilter.Build(framesPerSecond: 15, useSafeArea: false, sharpen: false),
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheSafeAreaFilterChainLeavesTheBorderClear()
    {
        string filter = CdgVideoFilter.Build(framesPerSecond: 1, useSafeArea: true);

        Assert.Contains($"scale={CdgFormat.SafeWidth}:{CdgFormat.SafeHeight}:force_original_aspect_ratio=decrease", filter, StringComparison.Ordinal);
        Assert.Contains(
            $"pad={CdgFormat.Width}:{CdgFormat.Height}:(ow-iw)/2:(oh-ih)/2:color=black",
            filter,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheSafeAreaIsTheRasterWithoutItsBorder()
    {
        Assert.Equal(new FrameSize(288, 192), CdgVideoFilter.GetRasterSize(useSafeArea: true));
        Assert.Equal(new FrameSize(300, 216), CdgVideoFilter.GetRasterSize(useSafeArea: false));
    }
}
