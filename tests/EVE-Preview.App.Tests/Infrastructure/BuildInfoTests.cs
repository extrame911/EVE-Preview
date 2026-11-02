using EvePreview.App.Services;

namespace EvePreview.App.Tests;

public sealed class BuildInfoTests
{
    [Fact]
    public void Version_uses_only_the_three_public_version_components()
    {
        Assert.Matches(@"^\d+\.\d+\.\d+$", BuildInfo.Version);
    }
}
