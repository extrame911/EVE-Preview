using System.Windows.Controls;
using System.Windows.Input;

namespace EvePreview.App.Behaviors;

internal static class InputFocus
{
    public static void Release(TextBox textBox)
    {
        FocusManager.SetFocusedElement(FocusManager.GetFocusScope(textBox), null);
        Keyboard.ClearFocus();
    }
}
