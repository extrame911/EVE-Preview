namespace EvePreview.App.Services;

/// <summary>
/// App-owned integration boundary. Core/Windows adapters implement this interface;
/// the UI shell has no dependency on their configuration or native types.
/// </summary>
public interface IPreviewRuntime
{
    public Task StartAsync(CancellationToken cancellationToken = default);
    public Task StopAsync(CancellationToken cancellationToken = default);
}
