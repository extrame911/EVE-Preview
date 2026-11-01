using EvePreview.Core;

namespace EvePreview.Core.Tests;

public sealed class ClientIdentityTests
{
    [Fact]
    public void ReusedProcessAndWindowHandleWithDifferentStartTimeIsDifferentIdentity()
    {
        var original = new ClientIdentity(42, 1234, DateTime.Parse("2026-01-01T00:00:00Z"));
        var reused = original with { ProcessStartTimeUtc = DateTime.Parse("2026-01-01T00:01:00Z") };

        Assert.NotEqual(original, reused);
    }
}
