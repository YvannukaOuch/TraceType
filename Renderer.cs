using SkiaSharp;

namespace TraceType;

public readonly record struct RenderResult(byte[] Png, YoloLabel Label);

/// <summary>
/// Turns mutated nodes into a PNG plus a correct YOLO label.
/// Applies endpoint "human error" geometry at draw time — overshoot/undershoot, lead-in/out
/// hooks, and imperfect loop closure (crossover or gap). These are biased per-writer
/// (structured error), not just random jitter. Because they're added before the bounding
/// box is computed, the label still covers all the real ink.
/// Every SkiaSharp object is unmanaged and disposed via `using`.
/// </summary>
public static class Renderer
{
    public const int CanvasSize = 64;
    private const float Margin = 7f;       // a touch more headroom for overshoot
    private const float MinWidth = 1.4f;   // fast strokes (thin)
    private const float MaxWidth = 4.2f;   // slow strokes (thick)

    public static RenderResult Render(
        ReadOnlySpan<Node> glyph, ReadOnlySpan<StrokeSpan> strokes, int classId, in WriterProfile profile)
    {
        // ---- fit content into the canvas ----
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        for (int i = 0; i < glyph.Length; i++)
        {
            minX = MathF.Min(minX, glyph[i].X); maxX = MathF.Max(maxX, glyph[i].X);
            minY = MathF.Min(minY, glyph[i].Y); maxY = MathF.Max(maxY, glyph[i].Y);
        }
        float spanX = MathF.Max(maxX - minX, 1e-4f);
        float spanY = MathF.Max(maxY - minY, 1e-4f);
        float target = CanvasSize - 2 * Margin;
        float scale = target / MathF.Max(spanX, spanY);
        float offX = Margin + (target - spanX * scale) * 0.5f;
        float offY = Margin + (target - spanY * scale) * 0.5f;

        (float x, float y) ToPixel(in Node n) =>
            (offX + (n.X - minX) * scale, offY + (n.Y - minY) * scale);

        var info = new SKImageInfo(CanvasSize, CanvasSize, SKColorType.Gray8, SKAlphaType.Opaque);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        using var ink = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            Color = new SKColor(20, 20, 20),
        };

        using var envelope = new SKPath();

        for (int si = 0; si < strokes.Length; si++)
        {
            var s = strokes[si];
            var stroke = glyph.Slice(s.Offset, s.Length);
            if (stroke.Length == 0) continue;

            // pixel polyline + per-point velocity
            int m = stroke.Length;
            var px = new float[m]; var py = new float[m]; var pv = new float[m];
            for (int i = 0; i < m; i++) { (px[i], py[i]) = ToPixel(stroke[i]); pv[i] = stroke[i].V; }

            // apply the endpoint imperfections (may grow/shrink the polyline)
            var (ex, ey, ev) = Imperfections.Apply(px, py, pv, profile, si);

            // draw variable-width segments
            for (int i = 0; i < ex.Count - 1; i++)
            {
                float avgV = (ev[i] + ev[i + 1]) * 0.5f;
                ink.StrokeWidth = MaxWidth + (MinWidth - MaxWidth) * avgV;
                canvas.DrawLine(ex[i], ey[i], ex[i + 1], ey[i + 1], ink);
            }

            // add to bbox envelope
            if (ex.Count >= 1)
            {
                envelope.MoveTo(ex[0], ey[0]);
                for (int i = 1; i < ex.Count; i++) envelope.LineTo(ex[i], ey[i]);
                if (ex.Count == 1) envelope.LineTo(ex[0], ey[0]);
            }
        }

        // ---- inked bbox: stroke the envelope at MAX width, take tight bounds ----
        using var boundsPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = MaxWidth,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
        };
        using var filled = new SKPath();
        boundsPaint.GetFillPath(envelope, filled);
        SKRect r = filled.TightBounds;

        var label = YoloLabel.FromPixelRect(classId, r.Left, r.Top, r.Right, r.Bottom, CanvasSize, CanvasSize);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return new RenderResult(data.ToArray(), label);
    }
}
