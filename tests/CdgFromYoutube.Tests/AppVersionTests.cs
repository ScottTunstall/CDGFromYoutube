namespace CdgFromYoutube.Tests;

public sealed class AppVersionTests
{
    [Fact]
    public void TheVersionLooksLikeASemanticVersion() =>
        Assert.Matches(@"^\d+\.\d+\.\d+$", AppVersion.Current);

    [Fact]
    public void TheBannerNamesTheProgramAndItsVersion() =>
        Assert.Equal($"{AppVersion.Name} ({AppVersion.Current})", AppVersion.Banner);
}
