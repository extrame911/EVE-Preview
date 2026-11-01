using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EvePreview.App.ViewModels;

public sealed class CycleGroupClientViewModel(string displayName, string? originalTitle = null)
    : INotifyPropertyChanged
{
    private bool _canMoveUp;
    private bool _canMoveDown;

    public string DisplayName { get; } = displayName;
    public string OriginalTitle { get; } = originalTitle ?? displayName;

    public bool CanMoveUp
    {
        get => _canMoveUp;
        set => SetField(ref _canMoveUp, value);
    }

    public bool CanMoveDown
    {
        get => _canMoveDown;
        set => SetField(ref _canMoveDown, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField(ref bool field, bool value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
