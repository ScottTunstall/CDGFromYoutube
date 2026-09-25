using NAudio.Lame;
using NAudio.Wave;

namespace CdgFromYoutube.Media;

/// <summary>Encodes raw sixteen bit PCM into MP3 with the LAME encoder.</summary>
/// <remarks>
/// LAME arrives with the NAudio.Lame package, which carries both the 32 and 64 bit builds of libmp3lame
/// and picks the right one at run time.
/// </remarks>
public sealed class LameMp3Encoder(int sampleRate, int channelCount, int bitRateKbps)
{
    /// <summary>The number of bits in each sample of the PCM that is fed in.</summary>
    public const int BitsPerSample = 16;

    /// <summary>How much PCM is read from the source before it is handed to the encoder.</summary>
    private const int ReadBufferSizeBytes = 64 * 1024;

    /// <summary>Reads PCM from one stream and writes MP3 frames to another.</summary>
    /// <param name="pcmStream">The stream of sixteen bit PCM to read.</param>
    /// <param name="mp3Stream">The stream the MP3 is written to.</param>
    /// <param name="cancellationToken">Cancels the encoding.</param>
    public async Task EncodeAsync(Stream pcmStream, Stream mp3Stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pcmStream);
        ArgumentNullException.ThrowIfNull(mp3Stream);

        WaveFormat format = new(sampleRate, BitsPerSample, channelCount);
        using LameMP3FileWriter writer = new(mp3Stream, format, bitRateKbps);

        byte[] buffer = new byte[ReadBufferSizeBytes];
        int read;
        while ((read = await pcmStream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
        {
            writer.Write(buffer, 0, read);
        }
    }
}
