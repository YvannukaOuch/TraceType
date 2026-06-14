using System.Formats.Tar;

namespace TraceType;

/// <summary>
/// Opt-in post-generation step. Extracts every shard-*.tar in a directory into a
/// browsable / directly-trainable layout:
///   &lt;dest&gt;/images/*.png   &lt;dest&gt;/labels/*.txt   (YOLO/PaddleOCR convention)
///
/// Note: tar is an archive, not compression — this is plain extraction. Keep it opt-in,
/// because at very large sample counts a flat folder of millions of tiny files is exactly
/// what the shards were avoiding. For a few thousand it's perfectly convenient.
/// (Want one mixed folder instead? Point both targets at the same dir.)
/// </summary>
public static class Unpacker
{
    public static (int images, int labels) UnpackAll(string shardDir, string destDir)
    {
        string imgDir = Path.Combine(destDir, "images");
        string lblDir = Path.Combine(destDir, "labels");
        Directory.CreateDirectory(imgDir);
        Directory.CreateDirectory(lblDir);

        int images = 0, labels = 0;
        var shards = Directory.EnumerateFiles(shardDir, "shard-*.tar").OrderBy(p => p);

        foreach (var tarPath in shards)
        {
            using var fs = File.OpenRead(tarPath);
            using var reader = new TarReader(fs);

            TarEntry? entry;
            while ((entry = reader.GetNextEntry()) is not null)
            {
                if (entry.EntryType != TarEntryType.RegularFile || entry.DataStream is null)
                    continue;

                string name = Path.GetFileName(entry.Name);
                bool isPng = name.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
                string target = Path.Combine(isPng ? imgDir : lblDir, name);

                using var outFs = File.Create(target);
                entry.DataStream.CopyTo(outFs);

                if (isPng) images++; else labels++;
            }
        }

        return (images, labels);
    }
}
