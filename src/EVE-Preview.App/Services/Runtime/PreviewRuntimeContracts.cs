using EvePreview.Core;

namespace EvePreview.App.Services;

public sealed record PreviewClientStatus(
    ClientIdentity Identity,
    string DisplayName,
    string OriginalWindowTitle,
    bool IsActive,
    bool IsPreviewVisible,
    bool IsExcludedFromCycle,
    string? PreviousWindowTitle = null,
    bool IsGameVisible = true,
    PreviewPlacement? Placement = null,
    double DpiScaleX = 1,
    double DpiScaleY = 1,
    bool HasSavedPlacement = false)
{
    public string ActivityText => IsGameVisible ? "游戏可见" : "游戏隐藏";
    public string VisibilityText => IsPreviewVisible ? "预览可见" : "预览隐藏";
    public string CycleStatusText => IsExcludedFromCycle ? "已排除出循环" : "参与循环";
}

public sealed record PreviewRuntimeSnapshot(
    IReadOnlyList<PreviewClientStatus> Clients,
    string? ActiveClientName,
    string? LastError,
    int PreviewWindowCount,
    int RegisteredHotkeyCount,
    bool CanDiscardUnsavedChanges = false,
    string? SaveError = null);

/// <summary>
/// Optional status surface for the UI shell. The shell remains usable when a runtime only implements lifecycle control.
/// </summary>
public interface IPreviewRuntimeStatusSource
{
    PreviewRuntimeSnapshot CurrentSnapshot { get; }
    event EventHandler<PreviewRuntimeSnapshot>? SnapshotChanged;
}

/// <summary>
/// Optional commands used by settings and preview interaction. Core configuration types stay out of the XAML layer.
/// </summary>
public interface IPreviewRuntimeController
{
    Task ApplySettingsAsync(AppSettings settings, CancellationToken cancellationToken = default);
    Task MinimizeAllAsync(CancellationToken cancellationToken = default);
    Task RestoreAllAsync(CancellationToken cancellationToken = default);
}

public interface IPreviewRuntimeSettingsRecovery
{
    Task<AppSettings> RestoreLastSavedSettingsAsync(
        AppSettings target,
        CancellationToken cancellationToken = default);
}

public sealed record ClientWindowLayoutBatchResult(
    IReadOnlyDictionary<string, PixelRectangle> SavedLayouts,
    int SkippedCount)
{
    public int SavedCount => SavedLayouts.Count;
}

/// <summary>
/// Saves and applies the allowed EVE client window placement (position and size only).
/// </summary>
public interface IClientWindowLayoutController
{
    Task<PixelRectangle?> SaveClientWindowLayoutAsync(
        string originalWindowTitle,
        CancellationToken cancellationToken = default);
    Task<ClientWindowLayoutBatchResult> SaveAllClientWindowLayoutsAsync(
        CancellationToken cancellationToken = default);
    Task<bool> ApplyClientWindowLayoutAsync(
        string originalWindowTitle,
        CancellationToken cancellationToken = default);
}
