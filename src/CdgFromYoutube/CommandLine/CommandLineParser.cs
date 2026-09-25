using System.Globalization;
using CdgFromYoutube.Imaging;
using CdgFromYoutube.Media;

namespace CdgFromYoutube.CommandLine;

/// <summary>Reads the command line into <see cref="KaraokeOptions"/>.</summary>
/// <remarks>
/// The parser is written by hand rather than taken from a library, because the set of options is small and
/// fixed and the messages can then name the option that is wrong and say what it expects.
/// </remarks>
public static class CommandLineParser
{
    private const string OutputOption = "--output";
    private const string NameOption = "--name";
    private const string FrameRateOption = "--fps";
    private const string BitRateOption = "--mp3-bitrate";
    private const string SampleRateOption = "--mp3-sample-rate";
    private const string MaximumSourceHeightOption = "--max-source-height";
    private const string DitherOption = "--dither";
    private const string SafeAreaOption = "--safe-area";
    private const string CropOption = "--crop";
    private const string AutoCropValue = "auto";
    private const string KeepTemporaryFilesOption = "--keep-temp";
    private const string FfmpegOption = "--ffmpeg";
    private const string YtDlpOption = "--yt-dlp";
    private const string JavaScriptRuntimeOption = "--js-runtime";
    private const string DownloadToolsOption = "--download-tools";
    private const string HelpOption = "--help";

    /// <summary>Reads a command line.</summary>
    public static ParseResult Parse(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        try
        {
            return ParseCore(arguments);
        }
        catch (CommandLineException exception)
        {
            return ParseResult.Failure(exception.Message);
        }
    }

    private static ParseResult ParseCore(IReadOnlyList<string> arguments)
    {
        string? url = null;
        string? outputDirectory = null;
        string? baseName = null;
        int frameRate = KaraokeOptions.DefaultVideoFrameRate;
        int bitRate = KaraokeOptions.DefaultMp3BitRateKbps;
        int? sampleRate = null;
        int? maximumSourceHeight = null;
        bool useDither = false;
        bool useSafeArea = false;
        CropMargins crop = CropMargins.None;
        bool autoCrop = false;
        bool keepTemporaryFiles = false;
        string? ffmpegPath = null;
        string? ytDlpPath = null;
        string? javaScriptRuntimePath = null;
        bool downloadTools = false;

        for (int index = 0; index < arguments.Count; index++)
        {
            string argument = arguments[index];
            switch (argument)
            {
                case "-h" or HelpOption:
                    return ParseResult.Help();

                case "-o" or OutputOption:
                    outputDirectory = ReadValue(arguments, argument, ref index);
                    break;

                case "-n" or NameOption:
                    baseName = ReadValue(arguments, argument, ref index);
                    break;

                case FrameRateOption:
                    frameRate = ReadInteger(
                        arguments,
                        argument,
                        ref index,
                        KaraokeOptions.MinimumVideoFrameRate,
                        KaraokeOptions.MaximumVideoFrameRate);
                    break;

                case BitRateOption:
                    bitRate = ReadInteger(
                        arguments,
                        argument,
                        ref index,
                        Mp3SampleRate.MinimumBitRateKbps,
                        Mp3SampleRate.Mpeg1MaximumBitRateKbps);
                    break;

                case SampleRateOption:
                    sampleRate = ReadInteger(arguments, argument, ref index, 0, int.MaxValue);
                    if (!Mp3SampleRate.IsSupported(sampleRate.Value))
                    {
                        throw new CommandLineException(
                            $"{SampleRateOption} must be one of {string.Join(", ", Mp3SampleRate.Supported)}.");
                    }

                    break;

                case MaximumSourceHeightOption:
                    maximumSourceHeight = ReadInteger(
                        arguments,
                        argument,
                        ref index,
                        1,
                        KaraokeOptions.MaximumSourceHeightPixels);
                    break;

                case DitherOption:
                    useDither = true;
                    break;

                case SafeAreaOption:
                    useSafeArea = true;
                    break;

                case CropOption:
                    (crop, autoCrop) = ReadCrop(arguments, argument, ref index);
                    break;

                case KeepTemporaryFilesOption:
                    keepTemporaryFiles = true;
                    break;

                case FfmpegOption:
                    ffmpegPath = ReadValue(arguments, argument, ref index);
                    break;

                case YtDlpOption:
                    ytDlpPath = ReadValue(arguments, argument, ref index);
                    break;

                case JavaScriptRuntimeOption:
                    javaScriptRuntimePath = ReadValue(arguments, argument, ref index);
                    break;

                case DownloadToolsOption:
                    downloadTools = true;
                    break;

                default:
                    if (argument.StartsWith('-'))
                    {
                        throw new CommandLineException($"'{argument}' is not an option this program knows.");
                    }

                    if (url is not null)
                    {
                        throw new CommandLineException("Only one video URL can be converted at a time.");
                    }

                    url = argument;
                    break;
            }
        }

        if (url is null)
        {
            throw new CommandLineException("A YouTube URL is needed.");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? videoUrl) ||
            (videoUrl.Scheme != Uri.UriSchemeHttp && videoUrl.Scheme != Uri.UriSchemeHttps))
        {
            throw new CommandLineException($"'{url}' is not an http or https address.");
        }

        return ParseResult.Success(new KaraokeOptions
        {
            VideoUrl = videoUrl,
            OutputDirectory = Path.GetFullPath(outputDirectory ?? Environment.CurrentDirectory),
            BaseName = baseName,
            VideoFrameRate = frameRate,
            Mp3BitRateKbps = bitRate,
            Mp3SampleRate = sampleRate,
            MaximumSourceHeight = maximumSourceHeight,
            UseDither = useDither,
            UseSafeArea = useSafeArea,
            Crop = crop,
            AutoCrop = autoCrop,
            KeepTemporaryFiles = keepTemporaryFiles,
            FfmpegPath = ffmpegPath,
            YtDlpPath = ytDlpPath,
            JavaScriptRuntimePath = javaScriptRuntimePath,
            DownloadTools = downloadTools,
        });
    }

    private static string ReadValue(IReadOnlyList<string> arguments, string option, ref int index)
    {
        if (index + 1 >= arguments.Count)
        {
            throw new CommandLineException($"{option} needs a value.");
        }

        index++;
        return arguments[index];
    }

    private static int ReadInteger(
        IReadOnlyList<string> arguments,
        string option,
        ref int index,
        int minimum,
        int maximum)
    {
        string text = ReadValue(arguments, option, ref index);
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            throw new CommandLineException($"{option} needs a whole number, but '{text}' was given.");
        }

        if (value < minimum || value > maximum)
        {
            throw new CommandLineException(
                $"{option} must be between {minimum} and {maximum}, but {value} was given.");
        }

        return value;
    }

    /// <summary>Reads the four percentages of <c>--crop left,top,right,bottom</c>, or <c>auto</c>.</summary>
    private static (CropMargins Crop, bool Auto) ReadCrop(IReadOnlyList<string> arguments, string option, ref int index)
    {
        string text = ReadValue(arguments, option, ref index);
        if (text.Equals(AutoCropValue, StringComparison.OrdinalIgnoreCase))
        {
            return (CropMargins.None, true);
        }

        string[] parts = text.Split(',');
        int[] values = new int[parts.Length];
        for (int part = 0; part < parts.Length; part++)
        {
            if (!int.TryParse(parts[part].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out values[part]))
            {
                values = [];
                break;
            }
        }

        if (values.Length != 4)
        {
            throw new CommandLineException(
                $"{option} needs four whole percentages as left,top,right,bottom, but '{text}' was given.");
        }

        CropMargins crop = new(values[0], values[1], values[2], values[3]);
        if (!crop.IsValid)
        {
            throw new CommandLineException(
                $"{option} percentages cannot be negative, and each pair of opposite edges may cut at most " +
                $"{CropMargins.MaximumTotalPercent} percent together, but '{text}' was given.");
        }

        return (crop, false);
    }

    /// <summary>Reports a command line that cannot be read.</summary>
    private sealed class CommandLineException(string message) : Exception(message);
}
