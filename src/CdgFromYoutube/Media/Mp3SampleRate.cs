namespace CdgFromYoutube.Media;

/// <summary>
/// The sample rates that an MP3 file can carry, and the bit rates that go with each of them.
/// </summary>
/// <remarks>
/// MPEG layer III only accepts nine sample rates. A source above them has to be resampled down, and the
/// rate decides how high the bit rate may go.
/// </remarks>
public static class Mp3SampleRate
{
    private static readonly int[] RatesInHertz =
        [8000, 11025, 12000, 16000, 22050, 24000, 32000, 44100, 48000];

    /// <summary>The lowest rate that belongs to the first version of the standard.</summary>
    private const int Mpeg1MinimumRateInHertz = 32000;

    /// <summary>The lowest rate that belongs to the second version of the standard.</summary>
    private const int Mpeg2MinimumRateInHertz = 16000;

    /// <summary>The highest bit rate an MP3 at 32 kHz or above may use.</summary>
    public const int Mpeg1MaximumBitRateKbps = 320;

    /// <summary>The highest bit rate an MP3 at 16 kHz or above may use.</summary>
    public const int Mpeg2MaximumBitRateKbps = 160;

    /// <summary>The highest bit rate an MP3 below 16 kHz may use.</summary>
    public const int Mpeg25MaximumBitRateKbps = 64;

    /// <summary>The lowest bit rate any MP3 may use.</summary>
    public const int MinimumBitRateKbps = 8;

    /// <summary>The sample rates that an MP3 file can carry, lowest first.</summary>
    public static IReadOnlyList<int> Supported => RatesInHertz;

    /// <summary>Returns whether the given rate can be written to an MP3 file.</summary>
    public static bool IsSupported(int rateInHertz) => Array.IndexOf(RatesInHertz, rateInHertz) >= 0;

    /// <summary>
    /// Returns the highest rate that MP3 carries and that is no higher than the source, so that a source
    /// which is already acceptable keeps its rate and a source above them all is reduced.
    /// </summary>
    public static int SelectFrom(int sourceRateInHertz)
    {
        int selected = RatesInHertz[0];
        foreach (int rate in RatesInHertz)
        {
            if (rate <= sourceRateInHertz)
            {
                selected = rate;
            }
        }

        return selected;
    }

    /// <summary>Returns the highest bit rate that the given sample rate can carry.</summary>
    public static int GetMaximumBitRateKbps(int sampleRateInHertz) => sampleRateInHertz switch
    {
        >= Mpeg1MinimumRateInHertz => Mpeg1MaximumBitRateKbps,
        >= Mpeg2MinimumRateInHertz => Mpeg2MaximumBitRateKbps,
        _ => Mpeg25MaximumBitRateKbps,
    };
}
