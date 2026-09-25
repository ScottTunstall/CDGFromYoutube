using CdgFromYoutube.Media;

namespace CdgFromYoutube.Tests;

public sealed class MediaProbeTests
{
    private const string ProbeJson = """
        {
          "streams": [
            {
              "codec_type": "video",
              "width": 1920,
              "height": 1080,
              "avg_frame_rate": "30000/1001",
              "r_frame_rate": "30000/1001"
            },
            {
              "codec_type": "audio",
              "sample_rate": "48000",
              "channels": 2
            }
          ],
          "format": {
            "duration": "213.456000"
          }
        }
        """;

    [Fact]
    public void ParseReadsTheLengthSizeAndSampleRate()
    {
        MediaInfo info = MediaProbe.Parse(ProbeJson);

        Assert.Equal(1920, info.VideoWidth);
        Assert.Equal(1080, info.VideoHeight);
        Assert.Equal(30000.0 / 1001, info.VideoFrameRate, precision: 6);
        Assert.Equal(48000, info.AudioSampleRate);
        Assert.Equal(2, info.AudioChannelCount);
        Assert.Equal(213.456, info.Duration.TotalSeconds, precision: 3);
        Assert.True(info.HasVideo);
        Assert.True(info.HasAudio);
        Assert.Equal("1920x1080", info.VideoSize.ToString());
    }

    [Fact]
    public void ParseIgnoresCoverArtThatIsReportedAsVideo()
    {
        const string JsonWithCoverArt = """
            {
              "streams": [
                {
                  "codec_type": "video",
                  "width": 600,
                  "height": 600,
                  "disposition": { "attached_pic": 1 }
                },
                {
                  "codec_type": "video",
                  "width": 1280,
                  "height": 720,
                  "r_frame_rate": "25/1"
                },
                {
                  "codec_type": "audio",
                  "sample_rate": "44100",
                  "channels": 2
                }
              ],
              "format": { "duration": "180.000000" }
            }
            """;

        MediaInfo info = MediaProbe.Parse(JsonWithCoverArt);

        Assert.Equal(1280, info.VideoWidth);
        Assert.Equal(720, info.VideoHeight);
        Assert.Equal(25, info.VideoFrameRate);
    }

    [Fact]
    public void ParseReportsWhatIsMissingFromAPicturelessFile()
    {
        const string AudioOnlyJson = """
            {
              "streams": [ { "codec_type": "audio", "sample_rate": "44100", "channels": 1 } ],
              "format": { "duration": "12.5" }
            }
            """;

        MediaInfo info = MediaProbe.Parse(AudioOnlyJson);

        Assert.False(info.HasVideo);
        Assert.True(info.HasAudio);
        Assert.Equal(44100, info.AudioSampleRate);
    }

    [Fact]
    public void ParseHandlesDurationsThatAreNumbersRatherThanText()
    {
        const string Json = """
            {
              "streams": [],
              "format": { "duration": 60.25 }
            }
            """;

        Assert.Equal(60.25, MediaProbe.Parse(Json).Duration.TotalSeconds, precision: 3);
    }
}
