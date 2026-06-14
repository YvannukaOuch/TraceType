namespace TraceType;

/// <summary>
/// Adds "human error" geometry to a stroke's pixel polyline:
///   - open strokes: a lead-in hook + endpoint overshoot (sometimes undershoot)
///   - closed loops ('o','a','e','d'...): cross the closure over, or leave a small gap
/// All magnitudes are scaled by the writer's biases, so a given hand overshoots a
/// characteristic amount — structured error, not uniform noise. Extension points are
/// marked fast (thin) so tails taper naturally.
/// </summary>
public static class Imperfections
{
    public static (List<float> X, List<float> Y, List<float> V) Apply(
        float[] px, float[] py, float[] pv, in WriterProfile p, int strokeIndex)
    {
        var ex = new List<float>(px); var ey = new List<float>(py); var ev = new List<float>(pv);
        int m = px.Length;
        if (m < 2) return (ex, ey, ev);

        var rng = new Random(p.NoiseSeed * 131 + strokeIndex * 7919);
        float size = Renderer.CanvasSize;
        float Jit() => 0.6f + (float)rng.NextDouble() * 0.8f; // 0.6 .. 1.4

        static (float x, float y) Norm(float dx, float dy)
        {
            float L = MathF.Sqrt(dx * dx + dy * dy);
            return L < 1e-5f ? (0f, 0f) : (dx / L, dy / L);
        }

        float endDist = MathF.Sqrt((px[0] - px[m - 1]) * (px[0] - px[m - 1]) +
                                   (py[0] - py[m - 1]) * (py[0] - py[m - 1]));
        bool isLoop = endDist < size * 0.14f && m >= 6;

        if (isLoop)
        {
            float crossProb = Math.Clamp(0.5f + 0.5f * p.LoopCross, 0f, 1f);
            if (rng.NextDouble() < crossProb)
            {
                // crossover: continue past the start point along the loop's opening tangent
                var (tx, ty) = Norm(px[1] - px[0], py[1] - py[0]);
                float len = size * p.OvershootBias * Jit();
                ex.Add(px[0] + tx * len); ey.Add(py[0] + ty * len); ev.Add(1f);
            }
            else
            {
                // gap: pull the final point back so the loop doesn't quite close
                var (ix, iy) = Norm(px[m - 1] - px[m - 2], py[m - 1] - py[m - 2]);
                float glen = size * p.OvershootBias * 0.7f * Jit();
                ex[^1] = px[m - 1] - ix * glen; ey[^1] = py[m - 1] - iy * glen;
            }
            return (ex, ey, ev);
        }

        // ---- open stroke ----
        // lead-in hook at the start
        if (p.HookBias > 0.001f && rng.NextDouble() < 0.7)
        {
            var (bx, by) = Norm(px[0] - px[1], py[0] - py[1]); // backward from start
            float hlen = size * p.HookBias * Jit();
            float sign = rng.NextDouble() < 0.5 ? 1f : -1f;
            float hx = px[0] + bx * hlen + (-by) * hlen * 0.4f * sign;
            float hy = py[0] + by * hlen + (bx) * hlen * 0.4f * sign;
            ex.Insert(0, hx); ey.Insert(0, hy); ev.Insert(0, 1f);
        }

        // overshoot (mostly) or undershoot (sometimes) at the end
        {
            var (fx, fy) = Norm(px[m - 1] - px[m - 2], py[m - 1] - py[m - 2]); // forward past end
            float frac = (float)(rng.NextDouble() * 1.3 - 0.3);                // -0.3 .. 1.0
            float olen = size * p.OvershootBias * frac;
            if (olen >= 0.5f) { ex.Add(px[m - 1] + fx * olen); ey.Add(py[m - 1] + fy * olen); ev.Add(1f); }
            else if (olen <= -0.5f) { ex[^1] = px[m - 1] + fx * olen; ey[^1] = py[m - 1] + fy * olen; }
        }

        return (ex, ey, ev);
    }
}
