using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EvePreview.App.Controls;

public sealed class CutCornerPanel : HeaderedContentControl
{
    static CutCornerPanel()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(CutCornerPanel),
            new FrameworkPropertyMetadata(typeof(CutCornerPanel)));
    }

    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon),
        typeof(Geometry),
        typeof(CutCornerPanel),
        new FrameworkPropertyMetadata(null));

    public static readonly DependencyProperty SectionCodeProperty = DependencyProperty.Register(
        nameof(SectionCode),
        typeof(string),
        typeof(CutCornerPanel),
        new FrameworkPropertyMetadata(string.Empty));

    public Geometry? Icon
    {
        get => (Geometry?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string SectionCode
    {
        get => (string)GetValue(SectionCodeProperty);
        set => SetValue(SectionCodeProperty, value);
    }
}
