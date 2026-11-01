namespace EvePreview.App.Services;

public static class PreviewMonitorDpi
{
    public static (double X, double Y) GetPrimaryScale()
    {
        var primary = MonitorGeometry.FromPoint(100, 100);
        return primary is null ? (1, 1) : (primary.DpiScaleX, primary.DpiScaleY);
    }
}
