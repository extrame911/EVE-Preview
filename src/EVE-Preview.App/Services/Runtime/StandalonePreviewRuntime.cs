namespace EvePreview.App.Services;

/// <summary>Standalone shell runtime; the parent composition root can replace it.</summary>
public sealed class StandalonePreviewRuntime : IPreviewRuntime
{
    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ResumeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
