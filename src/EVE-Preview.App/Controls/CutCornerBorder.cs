using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EvePreview.App.Controls;

public sealed class CutCornerBorder : Border
{
    public static readonly DependencyProperty CutCornerSizeProperty = DependencyProperty.Register(
        nameof(CutCornerSize),
        typeof(double),
        typeof(CutCornerBorder),
        new FrameworkPropertyMetadata(15d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CutCornerBrushProperty = DependencyProperty.Register(
        nameof(CutCornerBrush),
        typeof(Brush),
        typeof(CutCornerBorder),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CutCornerStrokeProperty = DependencyProperty.Register(
        nameof(CutCornerStroke),
        typeof(Brush),
        typeof(CutCornerBorder),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public double CutCornerSize
    {
        get => (double)GetValue(CutCornerSizeProperty);
        set => SetValue(CutCornerSizeProperty, value);
    }

    public Brush? CutCornerBrush
    {
        get => (Brush?)GetValue(CutCornerBrushProperty);
        set => SetValue(CutCornerBrushProperty, value);
    }

    public Brush? CutCornerStroke
    {
        get => (Brush?)GetValue(CutCornerStrokeProperty);
        set => SetValue(CutCornerStrokeProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var cut = Math.Min(CutCornerSize, Math.Min(ActualWidth, ActualHeight) / 2);
        if (cut <= 0 || CutCornerBrush is null)
        {
            return;
        }

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(ActualWidth - cut, 0), true, true);
            context.LineTo(new Point(ActualWidth, 0), true, false);
            context.LineTo(new Point(ActualWidth, cut), true, false);
        }

        geometry.Freeze();
        drawingContext.DrawGeometry(CutCornerBrush, null, geometry);

        var stroke = CutCornerStroke ?? BorderBrush;
        if (stroke is not null)
        {
            var pen = new Pen(stroke, Math.Max(1, BorderThickness.Right));
            pen.Freeze();
            drawingContext.DrawLine(
                pen,
                new Point(ActualWidth - cut, 0.5),
                new Point(ActualWidth - 0.5, cut));
        }
    }
}
