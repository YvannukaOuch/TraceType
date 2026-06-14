using System.Collections.Concurrent;
using System.Diagnostics;

namespace TraceType;

internal static class Program
{
    // Two decoupled stages. Generation never touches the unpack layer; you run unpack
    // afterwards (or never) against a finished output directory.
    //   dotnet run -- generate --samples 1000 --seed 42 --out ./out
    //   dotnet run -- unpack   --out ./out
    private static int Main(string[] args)
    {
        string verb = (args.Length > 0 && !args[0].StartsWith("--")) ? args[0].ToLowerInvariant() : "generate";
        return verb switch
        {
            "generate" => RunGenerate(args),
            "unpack"   => RunUnpack(args),
            _ => Usage(verb),
        };
    }

    private static int Usage(string verb)
    {
        Console.WriteLine($"Unknown command '{verb}'. Use:");
        Console.WriteLine("  generate --samples N --seed S --out DIR [--char c] [--svg file]");
        Console.WriteLine("  unpack   --out DIR [--dest DIR]");
        return 2;
    }

    // ---- STAGE 1: generation (pure; writes shards only) -------------------
    private static int RunGenerate(string[] args)
    {
        int samples = GetInt(args, "--samples", 16);
        int seed = GetInt(args, "--seed", 42);
        string outDir = GetStr(args, "--out", "./out");
        string? svg = GetStrOrNull(args, "--svg");
        string charset = GetStr(args, "--charset", "abcdefghijklmnopqrstuvwxyz");

        Directory.CreateDirectory(outDir);

        // TEMPLATE INGESTION: once, cached in memory. One template per class (letter).
        GlyphTemplate[] templates = svg is null
            ? Ingestion.BuiltInAlphabet(charset)
            : new[] { Ingestion.ParseSvg(svg, charset.Length > 0 ? charset[0] : 'e', classId: 0) };

        // classes.txt: classId -> char, for the trainer.
        File.WriteAllLines(Path.Combine(outDir, "classes.txt"),
            templates.Select(t => t.Char.ToString()));

        int totalNodes = templates.Sum(t => t.NodeCount);
        Console.WriteLine($"Loaded {templates.Length} class(es): {new string(templates.Select(t => t.Char).ToArray())}");
        Console.WriteLine($"Generating {samples} sample(s) across the charset -> {outDir}  (master seed {seed})");

        var sw = Stopwatch.StartNew();
        long done = 0, failed = 0;
        int shardCounter = 0;
        var errors = new ConcurrentBag<string>();

        var ranges = Partitioner.Create(0, samples);
        Parallel.ForEach(ranges, range =>
        {
            int shardId = Interlocked.Increment(ref shardCounter);
            using var shard = new TarShardWriter(Path.Combine(outDir, $"shard-{shardId:D4}.tar"));

            for (int i = range.Item1; i < range.Item2; i++)
            {
                try
                {
                    // round-robin across the charset -> balanced, deterministic class spread
                    GlyphTemplate tmpl = templates[i % templates.Length];
                    Pipeline.GenerateSample(tmpl, i, seed, shard);
                    long n = Interlocked.Increment(ref done);
                    if (n % 5000 == 0)
                        Console.WriteLine($"  {n}/{samples}  ({n / sw.Elapsed.TotalSeconds:F0}/s)");
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref failed);
                    if (errors.Count < 20) errors.Add($"#{i}: {ex.Message}");
                }
            }
        });

        sw.Stop();
        Console.WriteLine($"Done: {done} ok, {failed} failed in {sw.Elapsed.TotalSeconds:F2}s " +
                          $"({done / Math.Max(sw.Elapsed.TotalSeconds, 1e-6):F0}/s).");
        Console.WriteLine($"Shards written to {outDir}. Run 'unpack --out {outDir}' to extract a folder.");
        foreach (var e in errors) Console.WriteLine($"  ERR {e}");

        return failed == 0 ? 0 : 1;
    }

    // ---- STAGE 2: unpack (separate layer over finished output) ------------
    private static int RunUnpack(string[] args)
    {
        string outDir = GetStr(args, "--out", "./out");
        string dest = GetStr(args, "--dest", Path.Combine(outDir, "dataset"));

        if (!Directory.Exists(outDir) || !Directory.EnumerateFiles(outDir, "shard-*.tar").Any())
        {
            Console.WriteLine($"No shard-*.tar found in '{outDir}'. Nothing to unpack.");
            return 1;
        }

        Console.WriteLine($"Unpacking {outDir}/shard-*.tar -> {dest}/{{images,labels}} ...");
        var sw = Stopwatch.StartNew();
        var (imgs, lbls) = Unpacker.UnpackAll(outDir, dest);
        sw.Stop();
        Console.WriteLine($"Unpacked {imgs} images + {lbls} labels in {sw.Elapsed.TotalSeconds:F2}s.");
        return 0;
    }

    // ---- arg helpers ----
    private static string GetStr(string[] a, string k, string def) => GetStrOrNull(a, k) ?? def;
    private static int GetInt(string[] a, string k, int def) =>
        int.TryParse(GetStrOrNull(a, k), out var v) ? v : def;
    private static string? GetStrOrNull(string[] a, string k)
    {
        int i = Array.IndexOf(a, k);
        return (i >= 0 && i + 1 < a.Length) ? a[i + 1] : null;
    }
}
