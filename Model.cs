namespace TraceType;

/// <summary>
/// A single point on the pen trajectory.
/// Deliberately a *mutable struct* (value type), not a record/class:
///  - blittable + contiguous in an array, which is what ArrayPool and any future
///    System.Numerics SIMD pass actually need;
///  - mutating fields in place costs zero heap allocations.
/// The price is that you must pass it around by ref/Span, never copy-and-forget.
/// </summary>
public struct Node
{
    public float X;
    public float Y;
    public float V; // normalised pen speed in [0,1]; 1 = fast (thin), 0 = slow (thick)

    public Node(float x, float y, float v = 0f)
    {
        X = x;
        Y = y;
        V = v;
    }
}

/// <summary>
/// One stroke = a contiguous run of nodes inside the glyph's flat node buffer.
/// Storing strokes as (offset,length) into one array keeps the whole glyph in a
/// single rented buffer, so the worker rents once and returns once.
/// </summary>
public readonly record struct StrokeSpan(int Offset, int Length);

/// <summary>
/// An immutable, parsed glyph held in the startup cache. Never mutated; each sample
/// copies its nodes into a rented buffer before perturbing them.
/// </summary>
public sealed class GlyphTemplate
{
    public required char Char { get; init; }
    public required int ClassId { get; init; }
    public required Node[] Nodes { get; init; }       // flattened, normalised to 0..1
    public required StrokeSpan[] Strokes { get; init; }

    public int NodeCount => Nodes.Length;
}

/// <summary>
/// The coherent "who is writing this" vector. Sampled ONCE per output sample from the
/// seed and applied identically to every stroke of the glyph (and later, every glyph of
/// a word) so the result reads like one hand, not a committee.
/// </summary>
public readonly record struct WriterProfile(
    float SlantRadians,     // affine shear
    float ScaleX,
    float ScaleY,
    float BaselineDrift,    // vertical wander
    float TremorAmplitude,  // how shaky the hand is
    float TremorFrequency,
    float OvershootBias,    // mean endpoint overshoot, as a fraction of glyph size
    float HookBias,         // mean lead-in/out hook size, fraction of glyph size
    float LoopCross,        // -1..1: >0 tends to cross loops over, <0 to leave a gap
    int NoiseSeed)
{
    public static WriterProfile FromSeed(int seed)
    {
        // A tiny deterministic RNG so the profile is a pure function of the seed.
        var rng = new Random(seed);
        float Range(float lo, float hi) => lo + (float)rng.NextDouble() * (hi - lo);

        return new WriterProfile(
            SlantRadians:    Range(-0.20f, 0.30f),   // ~ -11deg .. +17deg, right-lean biased
            ScaleX:          Range(0.92f, 1.08f),
            ScaleY:          Range(0.92f, 1.08f),
            BaselineDrift:   Range(-0.03f, 0.03f),
            TremorAmplitude: Range(0.004f, 0.018f),
            TremorFrequency: Range(2.5f, 5.5f),
            OvershootBias:   Range(0.03f, 0.10f),    // structured: a given hand overshoots a typical amount
            HookBias:        Range(0.00f, 0.05f),
            LoopCross:       Range(-0.6f, 0.9f),     // biased toward crossing loops over
            NoiseSeed:       seed);
    }
}

/// <summary>
/// YOLO-format label. YOLO is one line of normalised floats: class cx cy w h.
/// (If you'd rather have COCO, serialise this to JSON instead — same numbers.)
/// </summary>
public readonly record struct YoloLabel(int ClassId, float Cx, float Cy, float W, float H)
{
    /// <summary>Build from an inked pixel-space rect on a canvas of the given size.</summary>
    public static YoloLabel FromPixelRect(int classId, float left, float top, float right, float bottom, int canvasW, int canvasH)
    {
        float w = (right - left) / canvasW;
        float h = (bottom - top) / canvasH;
        float cx = (left + right) * 0.5f / canvasW;
        float cy = (top + bottom) * 0.5f / canvasH;
        return new YoloLabel(classId, cx, cy, w, h);
    }

    public string ToYoloLine() =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"{ClassId} {Cx:F6} {Cy:F6} {W:F6} {H:F6}");
}
