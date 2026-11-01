using System.Windows;
using EvePreview.Core;

namespace EvePreview.App.Services;

public static class PreviewPlacementLayout
{
    private const double Gap = 12;

    public static int FindFirstFreeSlot(IEnumerable<int> usedSlots)
    {
        var used = usedSlots as IReadOnlySet<int> ?? new HashSet<int>(usedSlots);
        var slot = 0;
        while (used.Contains(slot))
        {
            slot++;
        }

        return slot;
    }

    public static PreviewPlacement CreateDefault(
        Rect workArea,
        int index,
        int widthPixels,
        int heightPixels,
        double dpiScaleX,
        double dpiScaleY)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        var width = PreviewPixelSize.ToDip(widthPixels, dpiScaleX);
        var height = PreviewPixelSize.ToDip(heightPixels, dpiScaleY);
        var rows = Math.Max(1, (int)Math.Floor((workArea.Height - 48) / (height + Gap)));
        var column = index / rows;
        var row = index % rows;
        return new PreviewPlacement(
            Math.Max(workArea.Left + Gap, workArea.Right - width - Gap - column * (width + Gap)),
            workArea.Top + 24 + row * (height + Gap),
            width,
            height);
    }
}
