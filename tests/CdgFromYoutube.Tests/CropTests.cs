using CdgFromYoutube.Imaging;
using CdgFromYoutube.Media;

namespace CdgFromYoutube.Tests;

public sealed class CropTests
{
    private const int Width = 100;
    private const int Height = 50;

    [Fact]
    public void MarginsBecomeAResolutionIndependentCropFilter()
    {
        CropMargins crop = new(15, 10, 5, 20);

        Assert.Equal("crop=iw*0.8:ih*0.7:iw*0.15:ih*0.1", crop.ToFfmpegFilter());
        Assert.Equal(new FrameSize(1024, 504), crop.Apply(new FrameSize(1280, 720)));
    }

    [Fact]
    public void TheFilterChainCropsBeforeItScales()
    {
        string filter = CdgVideoFilter.Build(framesPerSecond: 15, useSafeArea: false, new CropMargins(10, 0, 10, 0));

        int cropAt = filter.IndexOf("crop=", StringComparison.Ordinal);
        int scaleAt = filter.IndexOf("scale=", StringComparison.Ordinal);
        Assert.InRange(cropAt, 0, scaleAt);
    }

    [Fact]
    public void WithoutMarginsTheFilterChainDoesNotCrop()
    {
        string filter = CdgVideoFilter.Build(framesPerSecond: 15, useSafeArea: false);

        Assert.DoesNotContain("crop=", filter, StringComparison.Ordinal);
    }

    [Fact]
    public void TheLyricAreaIsWhereThePictureKeepsChanging()
    {
        LyricAreaDetector detector = new(Width, Height);
        byte[] frame = new byte[Width * Height * 3];

        for (int sample = 0; sample < 20; sample++)
        {
            Array.Clear(frame);

            // A logo that never changes, in the top left corner.
            Fill(frame, firstX: 0, firstY: 0, width: 10, height: 10, value: 200);

            // Lyrics that come and go in the middle of the picture.
            if (sample % 2 == 0)
            {
                Fill(frame, firstX: 30, firstY: 20, width: 40, height: 10, value: 255);
            }

            // A title that is only shown once, across the whole width.
            if (sample == 1)
            {
                Fill(frame, firstX: 0, firstY: 40, width: Width, height: 5, value: 255);
            }

            detector.AddFrame(frame);
        }

        // The lyrics span 30..69 by 20..29: the unused 30 percent on each side, 40 percent above and 40
        // percent below, less the two percent kept as a border.
        Assert.Equal(new CropMargins(28, 38, 28, 38), detector.GetCropMargins());
    }

    [Fact]
    public void APictureThatNeverChangesIsNotCropped()
    {
        LyricAreaDetector detector = new(Width, Height);
        byte[] frame = new byte[Width * Height * 3];
        Fill(frame, firstX: 10, firstY: 10, width: 20, height: 20, value: 255);

        for (int sample = 0; sample < 10; sample++)
        {
            detector.AddFrame(frame);
        }

        Assert.True(detector.GetCropMargins().IsNone);
    }

    private static void Fill(byte[] frame, int firstX, int firstY, int width, int height, byte value)
    {
        for (int y = firstY; y < firstY + height; y++)
        {
            Array.Fill(frame, value, ((y * Width) + firstX) * 3, width * 3);
        }
    }
}
