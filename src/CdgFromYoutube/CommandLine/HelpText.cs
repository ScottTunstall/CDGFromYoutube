using CdgFromYoutube.Cdg;
using CdgFromYoutube.Media;

namespace CdgFromYoutube.CommandLine;

/// <summary>The text that describes how the program is used.</summary>
public static class HelpText
{
    /// <summary>The one line form of the usage.</summary>
    public const string Summary = "cdgfromyoutube <youtube-url> [options]";

    /// <summary>The full usage text.</summary>
    /// <remarks>
    /// A constant interpolated string can only hold other strings, and this text quotes the numbers the
    /// format is built on, so it is a static field rather than a constant.
    /// </remarks>
    public static readonly string Full = $"""
        Converts a YouTube video into a karaoke pair: a CD+G graphics file and the MP3 it is played with.

        Usage:
          {Summary}

        Options:
          -o, --output <folder>          Where the .cdg and .mp3 files are written (default: here)
          -n, --name <name>              Base name of the output files (default: the video title)
              --fps <1-{KaraokeOptions.MaximumVideoFrameRate}>               Frames per second taken from the video (default: {KaraokeOptions.DefaultVideoFrameRate})
              --mp3-bitrate <8-{Mp3SampleRate.Mpeg1MaximumBitRateKbps}>      MP3 bit rate in kilobits per second (default: {KaraokeOptions.DefaultMp3BitRateKbps})
              --mp3-sample-rate <hz>     MP3 sample rate (default: the source rate, reduced to suit MP3)
              --max-source-height <px>   Prefer a source video no taller than this
              --dither                   Mix the two colours inside a tile to soften gradients
              --antialias                Draw lyrics with smooth edges; try this first if the video is
                                         mostly lyrics on a plain dark background, though a new page
                                         takes about twice as long to appear
              --flat-colours             Draw each lyric colour as one solid colour, with no shades;
                                         crisper than --antialias, but curved and diagonal strokes
                                         show a visible pixel staircase
              --safe-area                Keep the image inside the 288x192 area that all players show
              --crop <auto|l,t,r,b>      Crop to the lyrics (auto), or cut these percentages from
                                         the left, top, right and bottom, so the lyrics are drawn bigger
              --keep-temp                Keep the downloaded video
              --ffmpeg <path>            Path to ffmpeg; ffprobe is expected beside it
              --yt-dlp <path>            Path to yt-dlp
              --js-runtime <path>        Path to Deno, which yt-dlp uses to reach some videos
              --download-tools           Fetch yt-dlp, ffmpeg and Deno into the program's tools folder
                                         when they are missing, without asking first
          -h, --help                     Show this text
          -v, --version                  Show the program's version

          Run with just --download-tools and no URL to fetch the tools without converting a video.
          The tools are fetched once and kept next to the program, so later runs from any folder find them.

        Notes:
          A player reads exactly {CdgFormat.PacketsPerSecond} packets per second, and a whole screen is {CdgFormat.TileCount} tiles, so
          replacing everything on screen takes three seconds. Frames that cannot be paid for out of the
          packets available at their moment are dropped, so the graphics look their best on material that
          changes little between frames and move in steps during fast motion.
        """;
}
