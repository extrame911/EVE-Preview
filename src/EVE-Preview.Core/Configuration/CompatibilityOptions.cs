namespace EvePreview.Core;

public sealed class CompatibilityOptions
{
    public RenderingMode RenderingMode { get; set; } = RenderingMode.Dwm;
    public bool UseGdiFallback { get; set; } = true;
    public int GdiFramesPerSecond { get; set; } = 1;
    public int NativeCallTimeoutMs { get; set; } = 500;
}
