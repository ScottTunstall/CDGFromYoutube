using CdgFromYoutube.Media;

namespace CdgFromYoutube.Tests;

public sealed class OutputNamingTests
{
    [Theory]
    [InlineData(null, "karaoke")]
    [InlineData("", "karaoke")]
    [InlineData("   ", "karaoke")]
    [InlineData("///", "karaoke")]
    [InlineData("  Hello   World  ", "Hello World")]
    [InlineData("A/B:C*D", "A B C D")]
    [InlineData("Trailing dots...", "Trailing dots")]
    public void ResolveBaseNameProducesUsableFileNames(string? title, string expected) =>
        Assert.Equal(expected, OutputNaming.ResolveBaseName(title));

    [Fact]
    public void LongTitlesAreCutDownToTheLimit()
    {
        string title = new('a', OutputNaming.MaximumLength + 50);

        string baseName = OutputNaming.ResolveBaseName(title);

        Assert.Equal(OutputNaming.MaximumLength, baseName.Length);
    }

    [Fact]
    public void ControlCharactersBecomeSpaces()
    {
        Assert.Equal("Line One Line Two", OutputNaming.ResolveBaseName("Line One\nLine Two"));
    }
}
