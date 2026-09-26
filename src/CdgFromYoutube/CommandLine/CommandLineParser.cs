using System.Globalization;
using CdgFromYoutube.Imaging;
using CdgFromYoutube.Media;

namespace CdgFromYoutube.CommandLine;

/// <summary>Reads the command line into <see cref="KaraokeOptions"/>.</summary>
/// <remarks>
/// The parser is written by hand rather than taken from a library, because the set of options is small and
/// fixed and the messages can then name the option that is wrong and say what it expects. Options are split
/// across two switches, <see cref="TryApplyOption"/> and <see cref="TryApplyMoreOptions"/>, purely to keep
/// each one short; which switch an option is in has no other meaning.
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
    private const string AntialiasOption = "--antialias";
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
        ParserState state = new();

        for (int index = 0; index < arguments.Count; index++)
        {
            string argument = arguments[index];
            if (argument is "-h" or HelpOption)
            {
                return ParseResult.Help();
            }

            if (!TryApplyOption(state, argument, arguments, ref index))
            {
                ApplyPositional(state, argument);
            }
        }

        return BuildResult(state);
    }

    /// <summary>The first half of the options. Falls through to <see cref="TryApplyMoreOptions"/>.</summary>
    private static bool TryApplyOption(ParserState state, string argument, IReadOnlyList<string> arguments, ref int index)
    {
        switch (argument)
        {
            case "-o" or OutputOption:
                state.OutputDirectory = ReadValue(arguments, argument, ref index);
                return true;

            case "-n" or NameOption:
                state.BaseName = ReadValue(arguments, argument, ref index);
                return true;

            case FrameRateOption:
                state.FrameRate = ReadInteger(
                    arguments,
                    argument,
                    ref index,
                    KaraokeOptions.MinimumVideoFrameRate,
                    KaraokeOptions.MaximumVideoFrameRate);
                return true;

            case BitRateOption:
                state.BitRate = ReadInteger(
                    arguments,
                    argument,
                    ref index,
                    Mp3SampleRate.MinimumBitRateKbps,
                    Mp3SampleRate.Mpeg1MaximumBitRateKbps);
                return true;

            case SampleRateOption:
                state.SampleRate = ReadSampleRate(arguments, argument, ref index);
                return true;

            case MaximumSourceHeightOption:
                state.MaximumSourceHeight = ReadInteger(
                    arguments,
                    argument,
                    ref index,
                    1,
                    KaraokeOptions.MaximumSourceHeightPixels);
                return true;

            case DitherOption:
                state.UseDither = true;
                return true;

            case SafeAreaOption:
                state.UseSafeArea = true;
                return true;

            default:
                return TryApplyMoreOptions(state, argument, arguments, ref index);
        }
    }

    /// <summary>The second half of the options, tried once <see cref="TryApplyOption"/> finds no match.</summary>
    private static bool TryApplyMoreOptions(ParserState state, string argument, IReadOnlyList<string> arguments, ref int index)
    {
        switch (argument)
        {
            case CropOption:
                (state.Crop, state.AutoCrop) = ReadCrop(arguments, argument, ref index);
                return true;

            case AntialiasOption:
                state.Antialias = true;
                return true;

            case KeepTemporaryFilesOption:
                state.KeepTemporaryFiles = true;
                return true;

            case FfmpegOption:
                state.FfmpegPath = ReadValue(arguments, argument, ref index);
                return true;

            case YtDlpOption:
                state.YtDlpPath = ReadValue(arguments, argument, ref index);
                return true;

            case JavaScriptRuntimeOption:
                state.JavaScriptRuntimePath = ReadValue(arguments, argument, ref index);
                return true;

            case DownloadToolsOption:
                state.DownloadTools = true;
                return true;

            default:
                return false;
        }
    }

    private static void ApplyPositional(ParserState state, string argument)
    {
        if (argument.StartsWith('-'))
        {
            throw new CommandLineException($"'{argument}' is not an option this program knows.");
        }

        if (state.Url is not null)
        {
            throw new CommandLineException("Only one video URL can be converted at a time.");
        }

        state.Url = argument;
    }

    private static ParseResult BuildResult(ParserState state)
    {
        if (state.Url is null)
        {
            if (state.DownloadTools)
            {
                return ParseResult.ForToolSetup(new ToolSetupOptions
                {
                    FfmpegPath = state.FfmpegPath,
                    YtDlpPath = state.YtDlpPath,
                    JavaScriptRuntimePath = state.JavaScriptRuntimePath,
                });
            }

            throw new CommandLineException("A YouTube URL is needed.");
        }

        if (!Uri.TryCreate(state.Url, UriKind.Absolute, out Uri? videoUrl) ||
            (videoUrl.Scheme != Uri.UriSchemeHttp && videoUrl.Scheme != Uri.UriSchemeHttps))
        {
            throw new CommandLineException($"'{state.Url}' is not an http or https address.");
        }

        return ParseResult.Success(new KaraokeOptions
        {
            VideoUrl = videoUrl,
            OutputDirectory = Path.GetFullPath(state.OutputDirectory ?? Environment.CurrentDirectory),
            BaseName = state.BaseName,
            VideoFrameRate = state.FrameRate,
            Mp3BitRateKbps = state.BitRate,
            Mp3SampleRate = state.SampleRate,
            MaximumSourceHeight = state.MaximumSourceHeight,
            UseDither = state.UseDither,
            Antialias = state.Antialias,
            UseSafeArea = state.UseSafeArea,
            Crop = state.Crop,
            AutoCrop = state.AutoCrop,
            KeepTemporaryFiles = state.KeepTemporaryFiles,
            FfmpegPath = state.FfmpegPath,
            YtDlpPath = state.YtDlpPath,
            JavaScriptRuntimePath = state.JavaScriptRuntimePath,
            DownloadTools = state.DownloadTools,
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

    private static int ReadSampleRate(IReadOnlyList<string> arguments, string option, ref int index)
    {
        int sampleRate = ReadInteger(arguments, option, ref index, 0, int.MaxValue);
        if (!Mp3SampleRate.IsSupported(sampleRate))
        {
            throw new CommandLineException(
                $"{SampleRateOption} must be one of {string.Join(", ", Mp3SampleRate.Supported)}.");
        }

        return sampleRate;
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

    /// <summary>The options collected so far, while the command line is being read.</summary>
    private sealed class ParserState
    {
        public string? Url;
        public string? OutputDirectory;
        public string? BaseName;
        public int FrameRate = KaraokeOptions.DefaultVideoFrameRate;
        public int BitRate = KaraokeOptions.DefaultMp3BitRateKbps;
        public int? SampleRate;
        public int? MaximumSourceHeight;
        public bool UseDither;
        public bool Antialias;
        public bool UseSafeArea;
        public CropMargins Crop = CropMargins.None;
        public bool AutoCrop;
        public bool KeepTemporaryFiles;
        public string? FfmpegPath;
        public string? YtDlpPath;
        public string? JavaScriptRuntimePath;
        public bool DownloadTools;
    }

    /// <summary>Reports a command line that cannot be read.</summary>
    private sealed class CommandLineException(string message) : Exception(message);
}
