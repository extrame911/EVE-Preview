namespace EvePreview.App.ViewModels;

public sealed class SettingChangedEventArgs(string settingName, object value) : EventArgs
{
    public string SettingName { get; } = settingName;
    public object Value { get; } = value;
}
