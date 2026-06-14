namespace TraceType;

/// <summary>
/// The math engine. Every method mutates the nodes in place on a Span&lt;Node&gt; — zero
/// allocations. Order matters and is fixed here:
///   1. Affine  (macro style: slant/scale/baseline — applied to the whole glyph)
///   2. Velocity (computed from curvature of the SMOOTH path, BEFORE tremor)
///   3. Tremor  (micro jitter — applied last so it doesn't pollute the velocity estimate)
/// Velocity is derived from curvature via the two-thirds power law (tight curves are
/// slower), NOT from inter-node distance — your resampling makes distances uniform, so
/// a distance-based speed would come out flat.
/// </summary>
public static class Kinematics
{
    public static void Apply(Span<Node> glyph, ReadOnlySpan<StrokeSpan> strokes, in WriterProfile p)
    {
        Affine(glyph, p);

        foreach (var s in strokes)
        {
            var stroke = glyph.Slice(s.Offset, s.Length);
            ComputeVelocityFromCurvature(stroke);
        }

        Tremor(glyph, p);
    }

    // ---- 1. Affine ---------------------------------------------------------
    // Shear about the vertical: x' = x + tan(theta) * y, then scale, then drift.
    // A plain loop today; this is the obvious spot to drop a System.Numerics
    // Vector<float> pass once it's a measured hot path.
    private static void Affine(Span<Node> glyph, in WriterProfile p)
    {
        float shear = MathF.Tan(p.SlantRadians);
        for (int i = 0; i < glyph.Length; i++)
        {
            ref Node n = ref glyph[i];
            float x = n.X * p.ScaleX;
            float y = n.Y * p.ScaleY;
            n.X = x + shear * y;
            n.Y = y + p.BaselineDrift * x; // tiny slope on the baseline
        }
    }

    // ---- 2. Velocity from curvature ---------------------------------------
    private static void ComputeVelocityFromCurvature(Span<Node> stroke)
    {
        int n = stroke.Length;
        if (n == 0) return;
        if (n <= 2)
        {
            for (int i = 0; i < n; i++) stroke[i].V = 1f; // short strokes: treat as fast
            return;
        }

        const float kMax = 60f; // curvature clamp; tune to taste

        for (int i = 0; i < n; i++)
        {
            int a = Math.Max(i - 1, 0);
            int b = Math.Min(i + 1, n - 1);

            // Menger curvature of the triple (a, i, b): kappa = 4*Area / (|ab||bi||ia|)
            float kappa = MengerCurvature(stroke[a], stroke[i], stroke[b]);
            kappa = MathF.Min(kappa, kMax);

            // Two-thirds power law: tangential speed ~ radius^(1/3) = kappa^(-1/3).
            // Map to a normalised [0,1] where high curvature => low V.
            float speed = MathF.Pow(1f / (kappa + 1f), 1f / 3f);
            stroke[i].V = Math.Clamp(speed, 0f, 1f);
        }
    }

    private static float MengerCurvature(in Node p0, in Node p1, in Node p2)
    {
        float ax = p1.X - p0.X, ay = p1.Y - p0.Y;
        float bx = p2.X - p1.X, by = p2.Y - p1.Y;
        float cx = p2.X - p0.X, cy = p2.Y - p0.Y;

        float cross = MathF.Abs(ax * by - ay * bx);   // 2 * triangle area
        float la = MathF.Sqrt(ax * ax + ay * ay);
        float lb = MathF.Sqrt(bx * bx + by * by);
        float lc = MathF.Sqrt(cx * cx + cy * cy);
        float denom = la * lb * lc;
        if (denom < 1e-6f) return 0f;
        return 2f * cross / denom;
    }

    // ---- 3. Tremor --------------------------------------------------------
    // Continuous noise indexed by the node's position in the chain, so neighbouring
    // nodes wobble together. Amplitude/frequency come from the writer profile.
    private static void Tremor(Span<Node> glyph, in WriterProfile p)
    {
        for (int i = 0; i < glyph.Length; i++)
        {
            float t = i * p.TremorFrequency * 0.1f;
            float dx = ValueNoise.Sample(p.NoiseSeed, t) * p.TremorAmplitude;
            float dy = ValueNoise.Sample(p.NoiseSeed + 9173, t) * p.TremorAmplitude;
            glyph[i].X += dx;
            glyph[i].Y += dy;
        }
    }
}
