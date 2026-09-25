using System.Globalization;
using System.Text.Json;
using CdgFromYoutube.Tooling;

namespace CdgFromYoutube.Media;

/// <summary>Reads the duration, video size and audio sample rate of a media file with ffprobe.</summary>
public sealed class MediaProbe(ExternalTool ffprobe)
{
    private static readonly string[] ProbeArguments =
        ["-v", "error", "-print_format", "json", "-show_format", "-show_streams"];

    /// <summary>Probes a media file.</summary>
    public async Task<MediaInfo> ProbeAsync(string mediaPath, CancellationToken cancellationToken)
    {
        List<string> arguments = [.. ProbeArguments, mediaPath];
        ExternalToolResult result = await ffprobe.RunAsync(arguments, cancellationToken).ConfigureAwait(false);
        return Parse(result.StandardOutput);
    }

    /// <summary>Reads the properties of a file out of the JSON that ffprobe writes.</summary>
    public static MediaInfo Parse(string ffprobeJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ffprobeJson);

        using JsonDocument document = JsonDocument.Parse(ffprobeJson);
        JsonElement root = document.RootElement;

        TimeSpan duration = root.TryGetProperty("format", out JsonElement format) &&
            format.TryGetProperty("duration", out JsonElement durationElement)
                ? ParseSeconds(durationElement)
                : TimeSpan.Zero;

        int videoWidth = 0;
        int videoHeight = 0;
        double videoFrameRate = 0;
        int audioSampleRate = 0;
        int audioChannelCount = 0;

        if (root.TryGetProperty("streams", out JsonElement streams) && streams.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement stream in streams.EnumerateArray())
            {
                if (IsAttachedPicture(stream))
                {
                    // Cover art is reported as a video stream and would otherwise be mistaken for the video.
                    continue;
                }

                string codecType = GetText(stream, "codec_type");
                if (codecType == "video" && videoWidth == 0)
                {
                    videoWidth = GetInteger(stream, "width");
                    videoHeight = GetInteger(stream, "height");
                    videoFrameRate = ParseFrameRate(stream);
                }
                else if (codecType == "audio" && audioSampleRate == 0)
                {
                    audioSampleRate = GetInteger(stream, "sample_rate");
                    audioChannelCount = GetInteger(stream, "channels");
                }
            }
        }

        return new MediaInfo
        {
            Duration = duration,
            VideoWidth = videoWidth,
            VideoHeight = videoHeight,
            VideoFrameRate = videoFrameRate,
            AudioSampleRate = audioSampleRate,
            AudioChannelCount = audioChannelCount,
        };
    }

    private static bool IsAttachedPicture(JsonElement stream) =>
        stream.TryGetProperty("disposition", out JsonElement disposition) &&
        disposition.TryGetProperty("attached_pic", out JsonElement attachedPicture) &&
        attachedPicture.TryGetInt32(out int isAttachedPicture) &&
        isAttachedPicture == 1;

    private static TimeSpan ParseSeconds(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out double number))
        {
            return TimeSpan.FromSeconds(number);
        }

        if (element.ValueKind == JsonValueKind.String &&
            double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double text))
        {
            return TimeSpan.FromSeconds(text);
        }

        return TimeSpan.Zero;
    }

    private static double ParseFrameRate(JsonElement stream)
    {
        double frameRate = ParseFraction(GetText(stream, "avg_frame_rate"));
        return frameRate > 0 ? frameRate : ParseFraction(GetText(stream, "r_frame_rate"));
    }

    private static double ParseFraction(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        int separator = text.IndexOf('/', StringComparison.Ordinal);
        if (separator < 0)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : 0;
        }

        if (!double.TryParse(text[..separator], NumberStyles.Float, CultureInfo.InvariantCulture, out double numerator) ||
            !double.TryParse(text[(separator + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out double denominator))
        {
            return 0;
        }

        return denominator == 0 ? 0 : numerator / denominator;
    }

    private static string GetText(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;

    /// <summary>Reads a number that ffprobe may report either as a number or as text.</summary>
    private static int GetInteger(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement property))
        {
            return 0;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out int number))
        {
            return number;
        }

        return property.ValueKind == JsonValueKind.String &&
            int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int text)
                ? text
                : 0;
    }
}
