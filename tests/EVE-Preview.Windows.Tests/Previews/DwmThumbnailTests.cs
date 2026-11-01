namespace EvePreview.Windows.Tests;

public sealed class DwmThumbnailTests
{
    [Fact]
    public void Update_reuses_registration_and_dispose_unregisters_once()
    {
        var native = new FakeDwmNative { RegistrationResult = new NativeDwmRegistration(0, (nint)555) };
        var thumbnail = new DwmThumbnail((nint)11, (nint)22, native);
        var properties = new DwmThumbnailProperties(new WindowBounds(1, 2, 300, 200));

        thumbnail.Update(properties);
        thumbnail.Update(properties with { Opacity = 200 });
        thumbnail.Dispose();
        thumbnail.Dispose();

        Assert.Equal(1, native.RegisterCount);
        Assert.Equal(2, native.UpdatedProperties.Count);
        Assert.All(native.UpdatedProperties, update => Assert.Equal((nint)555, update.Handle));
        Assert.Equal(1, native.UnregisterCount);
        Assert.Equal((nint)555, native.UnregisteredHandle);
    }

    private sealed class FakeDwmNative : IDwmNative
    {
        public NativeDwmRegistration RegistrationResult { get; set; }
        public int RegisterCount { get; private set; }
        public int UnregisterCount { get; private set; }
        public nint UnregisteredHandle { get; private set; }
        public List<(nint Handle, DwmThumbnailProperties Properties)> UpdatedProperties { get; } = [];

        public NativeDwmRegistration Register(nint sourceWindow, nint destinationWindow)
        {
            RegisterCount++;
            return RegistrationResult;
        }

        public int Update(nint thumbnailHandle, DwmThumbnailProperties properties)
        {
            UpdatedProperties.Add((thumbnailHandle, properties));
            return 0;
        }

        public void Unregister(nint thumbnailHandle)
        {
            UnregisterCount++;
            UnregisteredHandle = thumbnailHandle;
        }
    }
}
