using System.Diagnostics;
using System.Globalization;

namespace CdgFromYoutube;

/// <summary>
/// Writes conversion progress to the console, keeping the progress bar on a single rewritten line.
/// </summary>
public sealed class ConsoleProgressSink : IProgressSink
{
    /// <summary>The number of characters in the progress bar itself.</summary>
    private const int ProgressBarWidth = 28;

    /// <summary>
    /// The shortest gap between two redraws of the progress line. Video decoding offers a frame far more
    /// often than a person can read, and redrawing for each of them makes the output hard to follow and
    /// slow to render.
    /// </summary>
    private const int MinimumRedrawIntervalMilliseconds = 100;

    private readonly bool _showProgressBar;
    private readonly Stopwatch _redrawTimer = Stopwatch.StartNew();
    private bool _progressLineOpen;

    /// <summary>Creates a sink that writes to standard output.</summary>
    public ConsoleProgressSink()
    {
        _showProgressBar = !Console.IsOutputRedirected;
    }

    /// <inheritdoc />
    public void Stage(string message) => WriteLine($"{message}", ConsoleColor.Cyan);

    /// <inheritdoc />
    public void Detail(string message) => WriteLine($"   {message}");

    /// <inheritdoc />
    public void Warning(string message) => WriteLine($"   warning: {message}", ConsoleColor.Yellow);

    /// <inheritdoc />
    public void Progress(double fraction, string message)
    {
        if (!_showProgressBar)
        {
            return;
        }

        double clamped = Math.Clamp(fraction, 0, 1);
        if (clamped < 1 && _redrawTimer.ElapsedMilliseconds < MinimumRedrawIntervalMilliseconds)
        {
            return;
        }

        _redrawTimer.Restart();
        int filled = (int)Math.Round(clamped * ProgressBarWidth);
        string bar = string.Concat(new string('#', filled), new string('-', ProgressBarWidth - filled));
        string percent = (clamped * 100).ToString("0.0", CultureInfo.InvariantCulture).PadLeft(5);
        Console.Write($"\r{FitLine($"   [{bar}] {percent}%  {message}")}");
        _progressLineOpen = true;
    }

    /// <inheritdoc />
    public void FinishProgress()
    {
        if (!_progressLineOpen)
        {
            return;
        }

        Console.WriteLine();
        _progressLineOpen = false;
    }

    /// <inheritdoc />
    public void Completed(string message) => WriteLine($"\n{message}", ConsoleColor.Green);

    private void WriteLine(string message, ConsoleColor? color = null)
    {
        FinishProgress();
        if (color is null)
        {
            Console.WriteLine(message);
            return;
        }

        Console.ForegroundColor = color.Value;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    /// <summary>
    /// Trims a line to the width of the console, or pads it out, so that rewriting it always covers
    /// whatever the previous version left behind and never wraps onto a second line.
    /// </summary>
    private static string FitLine(string line)
    {
        int usableWidth = GetConsoleWidth() - 1;
        if (usableWidth < 1)
        {
            return line;
        }

        return line.Length > usableWidth ? line[..usableWidth] : line.PadRight(usableWidth);
    }

    private static int GetConsoleWidth()
    {
        try
        {
            return Console.WindowWidth;
        }
        catch (IOException)
        {
            // The output stream is not a console that can report how wide it is.
            return 0;
        }
    }
}
