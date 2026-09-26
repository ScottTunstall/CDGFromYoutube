namespace CdgFromYoutube.Tests;

public sealed class AppVersionTests
{
    [Fact]
    public void TheVersionLooksLikeASemanticVersion() =>
        Assert.Matches(@"^\d+\.\d+\.\d+$", AppVersion.Current);
}
