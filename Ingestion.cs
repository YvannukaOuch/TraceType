using System.Globalization;
using System.Xml.Linq;

namespace TraceType;

/// <summary>
/// Loads single-line glyph templates ONCE at startup into an in-memory cache.
/// Per-sample work copies from this cache; it never re-parses.
///
/// Two sources:
///  - ParseSvgPolylines: reads &lt;polyline&gt;/&lt;path(M/L only)&gt; from a single-line SVG.
///  - BuiltInSample:     a hand-coded 'e' so the vertical slice runs with no asset files.
///
/// When you add Khmer/Arabic later, you write a new parser here and nothing downstream
/// changes — that's the whole point of isolating ingestion.
/// </summary>
public static class Ingestion
{
    /// <summary>Resample a polyline to a uniform number of points by arc length.</summary>
    private static List<Node> Resample(List<(float x, float y)> pts, int count)
    {
        var outp = new List<Node>(count);
        if (pts.Count == 0) return outp;
        if (pts.Count == 1) { for (int i = 0; i < count; i++) outp.Add(new Node(pts[0].x, pts[0].y)); return outp; }

        // cumulative length
        var cum = new float[pts.Count];
        for (int i = 1; i < pts.Count; i++)
        {
            float dx = pts[i].x - pts[i - 1].x, dy = pts[i].y - pts[i - 1].y;
            cum[i] = cum[i - 1] + MathF.Sqrt(dx * dx + dy * dy);
        }
        float total = cum[^1];
        if (total < 1e-6f) { for (int i = 0; i < count; i++) outp.Add(new Node(pts[0].x, pts[0].y)); return outp; }

        for (int i = 0; i < count; i++)
        {
            float target = total * i / (count - 1);
            int seg = 1;
            while (seg < pts.Count && cum[seg] < target) seg++;
            seg = Math.Min(seg, pts.Count - 1);
            float segLen = cum[seg] - cum[seg - 1];
            float t = segLen < 1e-6f ? 0f : (target - cum[seg - 1]) / segLen;
            float x = pts[seg - 1].x + (pts[seg].x - pts[seg - 1].x) * t;
            float y = pts[seg - 1].y + (pts[seg].y - pts[seg - 1].y) * t;
            outp.Add(new Node(x, y));
        }
        return outp;
    }

    private static GlyphTemplate Build(char ch, int classId, List<List<(float, float)>> strokesRaw, int perStroke = 24)
    {
        // normalise the whole glyph to the unit box (0..1), preserving aspect
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var s in strokesRaw)
            foreach (var (x, y) in s)
            {
                minX = MathF.Min(minX, x); maxX = MathF.Max(maxX, x);
                minY = MathF.Min(minY, y); maxY = MathF.Max(maxY, y);
            }
        float span = MathF.Max(MathF.Max(maxX - minX, maxY - minY), 1e-4f);

        var nodes = new List<Node>();
        var spans = new List<StrokeSpan>();
        foreach (var s in strokesRaw)
        {
            var norm = new List<(float, float)>(s.Count);
            foreach (var (x, y) in s) norm.Add(((x - minX) / span, (y - minY) / span));
            var resampled = Resample(norm, perStroke);
            spans.Add(new StrokeSpan(nodes.Count, resampled.Count));
            nodes.AddRange(resampled);
        }

        return new GlyphTemplate { Char = ch, ClassId = classId, Nodes = nodes.ToArray(), Strokes = spans.ToArray() };
    }

    /// <summary>A built-in lowercase 'e' (centerline) so the slice runs with no files.</summary>
    public static GlyphTemplate BuiltInSampleE(int classId = 0)
    {
        // one continuous stroke: crossbar, then the loop, then the tail — rough but legible
        var stroke = new List<(float, float)>
        {
            (0.10f, 0.50f), (0.90f, 0.50f),               // crossbar (left->right)
            (0.90f, 0.30f), (0.70f, 0.12f), (0.40f, 0.10f),
            (0.15f, 0.22f), (0.08f, 0.50f), (0.12f, 0.78f),
            (0.38f, 0.92f), (0.66f, 0.90f), (0.86f, 0.74f) // tail opening down-right
        };
        return Build('e', classId, new List<List<(float, float)>> { stroke });
    }

    /// <summary>Build a template from the embedded Hershey flat-stroke data.</summary>
    public static GlyphTemplate BuildFromFlat(char ch, int classId, float[][] strokesFlat, int perStroke = 24)
    {
        var raw = new List<List<(float, float)>>(strokesFlat.Length);
        foreach (var st in strokesFlat)
        {
            var pts = new List<(float, float)>(st.Length / 2);
            for (int i = 0; i + 1 < st.Length; i += 2) pts.Add((st[i], st[i + 1]));
            if (pts.Count > 0) raw.Add(pts);
        }
        return Build(ch, classId, raw, perStroke);
    }

    /// <summary>
    /// Build one template per character in <paramref name="charset"/> from the embedded
    /// a-z Hershey alphabet. ClassId = index in the charset (so it lines up with classes.txt).
    /// </summary>
    public static GlyphTemplate[] BuiltInAlphabet(string charset)
    {
        var templates = new GlyphTemplate[charset.Length];
        for (int i = 0; i < charset.Length; i++)
        {
            char c = charset[i];
            if (!Alphabet.Hershey.TryGetValue(c, out var strokes))
                throw new InvalidDataException($"No built-in glyph for '{c}'. Built-in set is a-z.");
            templates[i] = BuildFromFlat(c, classId: i, strokes);
        }
        return templates;
    }

    /// <summary>
    /// Parse a single-line SVG containing &lt;polyline points="x,y x,y..."/&gt; elements,
    /// or &lt;path&gt; using only M/L commands. Each element becomes one stroke.
    /// </summary>
    public static GlyphTemplate ParseSvg(string path, char ch, int classId)
    {
        var doc = XDocument.Load(path);
        var ns = doc.Root?.Name.Namespace ?? XNamespace.None;
        var strokes = new List<List<(float, float)>>();

        foreach (var pl in doc.Descendants(ns + "polyline"))
        {
            var raw = (string?)pl.Attribute("points") ?? "";
            var pts = ParsePointList(raw);
            if (pts.Count >= 2) strokes.Add(pts);
        }
        foreach (var p in doc.Descendants(ns + "path"))
        {
            var d = (string?)p.Attribute("d") ?? "";
            var pts = ParseSimplePathData(d);
            if (pts.Count >= 2) strokes.Add(pts);
        }

        if (strokes.Count == 0)
            throw new InvalidDataException($"No usable polyline/path strokes found in '{path}'.");

        return Build(ch, classId, strokes);
    }

    private static List<(float, float)> ParsePointList(string raw)
    {
        var pts = new List<(float, float)>();
        var tokens = raw.Replace(',', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i + 1 < tokens.Length; i += 2)
            if (float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                float.TryParse(tokens[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
                pts.Add((x, y));
        return pts;
    }

    // Handles the common single-line subset: M x y  L x y  L x y ...
    private static List<(float, float)> ParseSimplePathData(string d)
    {
        var pts = new List<(float, float)>();
        var tokens = d.Replace(",", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < tokens.Length; i++)
        {
            char c = tokens[i].Length == 1 && char.IsLetter(tokens[i][0]) ? char.ToUpperInvariant(tokens[i][0]) : '\0';
            if (c == 'M' || c == 'L')
            {
                if (i + 2 < tokens.Length &&
                    float.TryParse(tokens[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                    float.TryParse(tokens[i + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
                {
                    pts.Add((x, y));
                    i += 2;
                }
            }
        }
        return pts;
    }
}
