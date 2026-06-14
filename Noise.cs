namespace TraceType;

/// <summary>
/// Minimal deterministic 1D value noise with cubic smoothing.
/// Continuous (adjacent samples warp cohesively, like a real wrist) and a pure
/// function of (seed, t) so every sample is reproducible. Not cryptographic, not
/// "true" Perlin — good enough for tremor, and zero dependencies.
/// Swap for a proper Perlin/OpenSimplex lib later if you want richer texture.
/// </summary>
public static class ValueNoise
{
    private static float Hash(int seed, int i)
    {
        // integer hash -> [-1, 1]
        unchecked
        {
            uint h = (uint)(i * 374761393 + seed * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h / (float)uint.MaxValue) * 2f - 1f;
        }
    }

    private static float Smooth(float t) => t * t * (3f - 2f * t); // smoothstep

    /// <summary>Sample the noise field at position t.</summary>
    public static float Sample(int seed, float t)
    {
        int i0 = (int)MathF.Floor(t);
        float frac = t - i0;
        float a = Hash(seed, i0);
        float b = Hash(seed, i0 + 1);
        return a + (b - a) * Smooth(frac);
    }
}
