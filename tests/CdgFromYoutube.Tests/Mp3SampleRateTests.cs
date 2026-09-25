using CdgFromYoutube.Media;

namespace CdgFromYoutube.Tests;

public sealed class Mp3SampleRateTests
{
    [Theory]
    [InlineData(96000, 48000)]
    [InlineData(48000, 48000)]
    [InlineData(44100, 44100)]
    [InlineData(22050, 22050)]
    [InlineData(44000, 32000)]
    [InlineData(4000, 8000)]
    public void SelectFromKeepsAcceptableRatesAndReducesTheRest(int sourceRate, int expectedRate) =>
        Assert.Equal(expectedRate, Mp3SampleRate.SelectFrom(sourceRate));

    [Theory]
    [InlineData(48000, 320)]
    [InlineData(44100, 320)]
    [InlineData(32000, 320)]
    [InlineData(24000, 160)]
    [InlineData(11025, 64)]
    public void GetMaximumBitRateKbpsFollowsTheStandard(int sampleRate, int expectedKbps) =>
        Assert.Equal(expectedKbps, Mp3SampleRate.GetMaximumBitRateKbps(sampleRate));

    [Theory]
    [InlineData(44100, true)]
    [InlineData(96000, false)]
    [InlineData(0, false)]
    public void IsSupportedMatchesTheRatesTheFormatCarries(int sampleRate, bool expected) =>
        Assert.Equal(expected, Mp3SampleRate.IsSupported(sampleRate));
}
