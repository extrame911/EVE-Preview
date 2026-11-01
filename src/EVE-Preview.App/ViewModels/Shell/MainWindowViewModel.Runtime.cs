using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvePreview.App.Services;
using EvePreview.Core;
using Microsoft.Win32;

namespace EvePreview.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    public event Action<string>? SaveFailurePromptRequested;

    private void OnRuntimeSnapshotChanged(object? sender, PreviewRuntimeSnapshot snapshot) =>
        ApplyRuntimeSnapshot(snapshot);

    private void ApplyRuntimeSnapshot(PreviewRuntimeSnapshot snapshot)
    {
        var visibleClients = snapshot.Clients
            .Where(client => !ClientWindowTitle.IsLoginWindow(client.OriginalWindowTitle))
            .ToArray();
        var visibleSnapshot = snapshot with
        {
            Clients = visibleClients,
            ActiveClientName = visibleClients.Any(client => client.IsActive)
                ? snapshot.ActiveClientName
                : null
        };
        Overview.ClientCount = visibleSnapshot.Clients.Count;
        Overview.ActiveClientText = visibleSnapshot.ActiveClientName ??
            LocalizationManager.Translate("未检测到活动客户端");
        Clients.ApplySnapshot(visibleSnapshot);
        CycleGroups.SyncOnlineClients(visibleSnapshot.Clients.Select(client => client.OriginalWindowTitle));
        Overview.Clients.Clear();
        foreach (var client in visibleSnapshot.Clients)
        {
            var (characterName, serverName) = SplitClientTitle(client.OriginalWindowTitle);
            Overview.Clients.Add(new ClientSummaryViewModel
            {
                DisplayName = client.DisplayName,
                OriginalWindowTitle = client.OriginalWindowTitle,
                CharacterName = characterName,
                ServerName = serverName,
                ActivityText = LocalizationManager.Translate(client.ActivityText),
                VisibilityText = LocalizationManager.Translate(client.VisibilityText),
                CycleStatusText = LocalizationManager.Translate(client.CycleStatusText)
            });
        }

        CanDiscardUnsavedChanges = snapshot.CanDiscardUnsavedChanges;
        if (!string.IsNullOrWhiteSpace(snapshot.LastError))
        {
            LastError = snapshot.LastError;
        }
        else if (!string.IsNullOrEmpty(_lastRuntimeError) &&
                 string.Equals(LastError, _lastRuntimeError, StringComparison.Ordinal))
        {
            LastError = string.Empty;
        }

        _lastRuntimeError = snapshot.LastError ?? string.Empty;
        UpdateSaveFailurePrompt(snapshot);
        SystemSettings.DiagnosticSummary = LocalizationManager.IsEnglish
            ? $"Preview windows {snapshot.PreviewWindowCount} · Registered hotkeys {snapshot.RegisteredHotkeyCount} · " +
              (string.IsNullOrWhiteSpace(snapshot.LastError)
                  ? "No native call errors"
                  : "Latest warning shown in the status bar")
            : $"预览窗口 {snapshot.PreviewWindowCount} 个 · 已注册热键 {snapshot.RegisteredHotkeyCount} 个 · " +
              (string.IsNullOrWhiteSpace(snapshot.LastError)
                  ? "无原生调用错误"
                  : "最近警告已显示在状态栏");
        UpdateStatusText();
    }

    private void UpdateSaveFailurePrompt(PreviewRuntimeSnapshot snapshot)
    {
        if (!snapshot.CanDiscardUnsavedChanges)
        {
            _saveFailurePromptShown = false;
            return;
        }

        if (_saveFailurePromptShown || string.IsNullOrWhiteSpace(snapshot.SaveError))
        {
            return;
        }

        _saveFailurePromptShown = true;
        SaveFailurePromptRequested?.Invoke(snapshot.SaveError);
    }

    private static (string CharacterName, string ServerName) SplitClientTitle(string title)
    {
        if (!ClientWindowTitle.TrySplit(title, out var serverName, out var characterName))
        {
            return ("[未登录]", title);
        }

        return (string.IsNullOrWhiteSpace(characterName) ? "[未登录]" : characterName, serverName);
    }

    private void UpdateStatusText()
    {
        OnPropertyChanged(nameof(ServiceStatusText));
    }
}
