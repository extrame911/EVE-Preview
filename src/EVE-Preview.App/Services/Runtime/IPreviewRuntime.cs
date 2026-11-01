namespace EvePreview.App.Services;

/// <summary>
/// App-owned integration boundary. Core/Windows adapters implement this interface;
/// the UI shell has no dependency on their configuration or native types.
/// </summary>
public interface IPreviewRuntime
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task PauseAsync(CancellationToken cancellationToken = default);
    Task ResumeAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
