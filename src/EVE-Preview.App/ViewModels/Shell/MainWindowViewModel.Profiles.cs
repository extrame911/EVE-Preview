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
    public void SetProfilePath(string path) =>
        SystemSettings.SetProfilePath(path);

    private void ReloadProfile()
    {
        if (_profiles is null)
        {
            return;
        }

        try
        {
            _profiles.RequestRestart(_profiles.CurrentPath, saveCurrentSettings: false);
        }
        catch (Exception exception)
        {
            SystemSettings.ProfileStatusText = $"重载失败：{exception.Message}";
        }
    }

    private void SaveProfileAs()
    {
        if (_profiles is null)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "EVE-Preview 配置 (*.json)|*.json",
            InitialDirectory = _profiles.DirectoryPath,
            FileName = Path.GetFileName(_profiles.CurrentPath),
            AddExtension = true,
            DefaultExt = ".json",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            ApplySettingsToModel();
            _profiles.SaveAs(dialog.FileName, _settings);
            SystemSettings.ProfileStatusText = $"已保存并切换到 {Path.GetFileName(_profiles.CurrentPath)}，即将重启。";
            _profiles.RequestRestart(_profiles.CurrentPath);
        }
        catch (Exception exception)
        {
            SystemSettings.ProfileStatusText = $"另存为失败：{exception.Message}";
        }
    }

    private void ImportLegacyProfile()
    {
        if (_profiles is null)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = "旧版 EVE-O-Preview 配置 (*.json)|*.json|所有文件 (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var result = _profiles.ImportLegacy(dialog.FileName);
            var importedName = $"EVE-Preview-imported-{DateTime.Now:yyyyMMdd-HHmmss}.json";
            _profiles.SaveAs(importedName, result.Settings);
            SystemSettings.ProfileStatusText = result.Warnings.Count == 0
                ? $"已导入 {importedName}，即将重启。"
                : $"已导入 {importedName}，有 {result.Warnings.Count} 个字段未映射，即将重启。";
            if (result.Warnings.Count > 0)
            {
                MessageBox.Show(
                    string.Join(Environment.NewLine, result.Warnings.Prepend("以下旧版字段未能映射：")),
                    "旧版配置导入",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            _profiles.RequestRestart(_profiles.CurrentPath, saveCurrentSettings: false);
        }
        catch (Exception exception)
        {
            SystemSettings.ProfileStatusText = $"导入失败：{exception.Message}";
        }
    }

    private void OpenProfileFolder()
    {
        try
        {
            _profiles?.OpenDirectory();
        }
        catch (Exception exception)
        {
            SystemSettings.ProfileStatusText = $"打开配置目录失败：{exception.Message}";
        }
    }

    private void SwitchProfile(string profile)
    {
        if (_profiles is null)
        {
            return;
        }

        try
        {
            _profiles.RequestRestart(_profiles.ResolveProfilePath(profile));
        }
        catch (Exception exception)
        {
            SystemSettings.ProfileStatusText = $"切换失败：{exception.Message}";
        }
    }
}
