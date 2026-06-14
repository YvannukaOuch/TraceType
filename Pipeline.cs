using System.Buffers;
using System.Formats.Tar;

namespace TraceType;

/// <summary>
/// One tar shard owned by ONE worker. Not thread-safe by design — each worker creates
/// its own, so there is no shared writer and no lock funnel. N workers => N shard files,
/// which is also exactly what dataset loaders (WebDataset-style) want.
/// </summary>
public sealed class TarShardWriter : IDisposable
{
    private readonly FileStream _fs;
    private readonly TarWriter _tar;

    public TarShardWriter(string path)
    {
        _fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 1 << 20);
        _tar = new TarWriter(_fs, TarEntryFormat.Pax, leaveOpen: false);
    }

    public void Write(string baseName, byte[] png, string yoloLine)
    {
        WriteBytes($"{baseName}.png", png);
        WriteBytes($"{baseName}.txt", System.Text.Encoding.ASCII.GetBytes(yoloLine + "\n"));
    }

    private void WriteBytes(string name, byte[] bytes)
    {
        var entry = new PaxTarEntry(TarEntryType.RegularFile, name)
        {
            DataStream = new MemoryStream(bytes, writable: false)
        };
        _tar.WriteEntry(entry);
    }

    public void Dispose()
    {
        _tar.Dispose(); // flushes
        _fs.Dispose();
    }
}

/// <summary>
/// The full per-sample chain, top to bottom, inside a single worker:
///   rent -> copy clean template -> kinematics (in place) -> render -> write -> return.
/// </summary>
public static class Pipeline
{
    public static void GenerateSample(GlyphTemplate tmpl, int sampleIndex, int masterSeed, TarShardWriter shard)
    {
        int seed = DeriveSeed(masterSeed, sampleIndex);
        int n = tmpl.NodeCount;

        Node[] buffer = ArrayPool<Node>.Shared.Rent(n);
        try
        {
            // Rent gives AT LEAST n (often more) -> always work on a span sliced to the
            // logical length, never buffer.Length.
            Span<Node> glyph = buffer.AsSpan(0, n);
            tmpl.Nodes.AsSpan().CopyTo(glyph);

            var profile = WriterProfile.FromSeed(seed);
            Kinematics.Apply(glyph, tmpl.Strokes, profile);

            RenderResult result = Renderer.Render(glyph, tmpl.Strokes, tmpl.ClassId, profile);
            shard.Write($"{tmpl.Char}_{(uint)seed}", result.Png, result.Label.ToYoloLine());
        }
        finally
        {
            // Node is a value type with no references -> no reason to clear (faster).
            ArrayPool<Node>.Shared.Return(buffer, clearArray: false);
        }
    }

    /// <summary>Reproducible per-sample seed: pure function of (master, index).</summary>
    public static int DeriveSeed(int master, int index)
    {
        unchecked
        {
            uint h = (uint)master * 2654435761u + (uint)index * 40503u;
            h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
            return (int)h;
        }
    }
}
