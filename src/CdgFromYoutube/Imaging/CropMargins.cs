using System.Globalization;

namespace CdgFromYoutube.Imaging;

/// <summary>
/// How much of each edge of the source video is cut away before it is scaled into the raster, as whole
/// percentages of the source width and height.
/// </summary>
/// <remarks>
/// The raster is only 300x216, so a lyric that fills a third of the source frame is drawn with letters about
/// eight pixels tall. Cutting away the margins the lyrics never reach lets the rest be scaled up, which gives
/// every letter more pixels and makes its edges less blocky.
/// </remarks>
/// <param name="Left">The percentage of the width cut from the left edge.</param>
/// <param name="Top">The percentage of the height cut from the top edge.</param>
/// <param name="Right">The percentage of the width cut from the right edge.</param>
/// <param name="Bottom">The percentage of the height cut from the bottom edge.</param>
public readonly record struct CropMargins(int Left, int Top, int Right, int Bottom)
{
    /// <summary>The most that may be cut from both edges of one direction together, in percent.</summary>
    public const int MaximumTotalPercent = 90;

    private const double PercentScale = 100.0;

    /// <summary>No cropping at all.</summary>
    public static CropMargins None => default;

    /// <summary>Whether nothing is cut away.</summary>
    public bool IsNone => this == None;

    /// <summary>Returns whether the margins are each at least zero and leave some of the picture in both directions.</summary>
    public bool IsValid =>
        Left >= 0 && Top >= 0 && Right >= 0 && Bottom >= 0 &&
        Left + Right <= MaximumTotalPercent && Top + Bottom <= MaximumTotalPercent;

    /// <summary>Returns the size a frame of the given size has once it is cropped.</summary>
    public FrameSize Apply(FrameSize source) => new(
        Math.Max(1, (int)Math.Round(source.Width * (1 - ((Left + Right) / PercentScale)))),
        Math.Max(1, (int)Math.Round(source.Height * (1 - ((Top + Bottom) / PercentScale)))));

    /// <summary>Returns the ffmpeg crop filter that cuts the margins away, whatever the size of the source.</summary>
    public string ToFfmpegFilter()
    {
        string keptWidth = Format(1 - ((Left + Right) / PercentScale));
        string keptHeight = Format(1 - ((Top + Bottom) / PercentScale));
        string x = Format(Left / PercentScale);
        string y = Format(Top / PercentScale);
        return $"crop=iw*{keptWidth}:ih*{keptHeight}:iw*{x}:ih*{y}";
    }

    /// <summary>Describes the margins as "left,top,right,bottom", the form the command line takes.</summary>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Left},{Top},{Right},{Bottom}");

    private static string Format(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}
