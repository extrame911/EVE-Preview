using System.Windows;
using EvePreview.App.Services;
using EvePreview.Core;
using EvePreview.Windows;

namespace EvePreview.App.Tests;

[Collection("WPF Application")]
public sealed class PreviewWindowVisibilityTests(WpfApplicationFixture wpf)
{
    [Fact]
    public void Login_client_preview_is_always_hidden()
    {
        wpf.Invoke(() =>
        {
            PreviewWindow? preview = null;
            try
            {
                const string loginTitle = "星战前夜：晨曦 [Infinity]";
                preview = new PreviewWindow(
                    new ProcessSnapshot(42, "exefile", 0, loginTitle, DateTime.Now),
                    loginTitle,
                    new AppSettings(),
                    new PreviewPlacement(0, 0, 320, 180),
                    (_, _) => { },
                    (_, _) => { });

                Assert.Equal(Visibility.Hidden, preview.Visibility);
            }
            finally
            {
                preview?.Dispose();
            }
        });
    }
}
