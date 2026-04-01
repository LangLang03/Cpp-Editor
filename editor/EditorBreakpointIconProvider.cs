namespace C__Editor;

internal static class EditorGutterIconIds
{
    internal const int BreakpointMarker = 1001;
    internal const int DebugExecutionPointer = 1002;
}

internal sealed class EditorBreakpointIconProvider : SweetEditor.EditorIconProvider
{
    private readonly Dictionary<int, Image> cache = new();

    public Image? GetIconImage(int iconId)
    {
        if (cache.TryGetValue(iconId, out var existing))
        {
            return existing;
        }

        Image? created = iconId switch
        {
            EditorGutterIconIds.BreakpointMarker => CreateBreakpointBitmap(),
            EditorGutterIconIds.DebugExecutionPointer => CreateExecutionPointerBitmap(),
            _ => null
        };

        if (created is null)
        {
            return null;
        }

        cache[iconId] = created;
        return created;
    }

    private static Image CreateBreakpointBitmap()
    {
        var size = 18;
        var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var circleRect = new RectangleF(2f, 2f, size - 5f, size - 5f);
        using var fillBrush = new SolidBrush(Color.FromArgb(220, 220, 48, 48));
        using var borderPen = new Pen(Color.FromArgb(235, 132, 18, 18), 1.4f);
        graphics.FillEllipse(fillBrush, circleRect);
        graphics.DrawEllipse(borderPen, circleRect);

        return bitmap;
    }

    private static Image CreateExecutionPointerBitmap()
    {
        var size = 18;
        var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var points = new[]
        {
            new PointF(3f, 3f),
            new PointF(size - 4f, size * 0.5f),
            new PointF(3f, size - 3f)
        };

        using var fillBrush = new SolidBrush(Color.FromArgb(232, 255, 210, 64));
        using var borderPen = new Pen(Color.FromArgb(240, 178, 126, 18), 1.4f);
        graphics.FillPolygon(fillBrush, points);
        graphics.DrawPolygon(borderPen, points);
        return bitmap;
    }
}
