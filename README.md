# TraceType

A kinematic stroke-trajectory handwriting synthesizer written in C#.

Generates realistic, perfectly-labeled handwriting samples for training OCR / HTR models.
The core idea: perturb the **pen trajectory** (not the outline), so the result moves and
looks like a real hand — variable stroke width, tremor, overshoots, hooks, and loop imperfections.

---

## How it works

```
Glyph template  →  Kinematics  →  Renderer  →  YOLO label  →  .tar shard
(centerline)       (in-place)     (SkiaSharp)   (bbox from ink)
```

1. **Ingest** a centerline glyph (built-in Hershey a–z, or your own SVG).
2. **Apply kinematics** — affine transform (slant/scale), curvature-based velocity (two-thirds power law), tremor noise.
3. **Add human imperfections** — endpoint overshoot/undershoot, lead-in hooks, loop crossover/gap — all biased per-writer so the output reads like one hand.
4. **Render** to a 64×64 grayscale PNG with variable-width ink (thick when slow, thin when fast).
5. **Emit** a YOLO label whose bounding box covers the actual ink, not the skeleton.
6. **Write** to a per-worker `.tar` shard — no shared-writer lock, no bottleneck.

---

## Quickstart

```bash
# Prerequisites: .NET 8 SDK

# Clone
git clone https://github.com/YOUR_USERNAME/TraceType.git
cd TraceType

# STAGE 1 — generate samples (writes shard-*.tar files)
dotnet run -c Release -- generate --samples 26000 --seed 42 --out ./out

# Generate a subset of letters
dotnet run -c Release -- generate --samples 5000 --seed 42 --charset aeiou --out ./out

# Use your own single-line SVG (one class)
dotnet run -c Release -- generate --samples 1000 --seed 42 --svg path/to/g.svg --out ./out

# STAGE 2 — unpack into images/ + labels/ folders (optional)
dotnet run -c Release -- unpack --out ./out
dotnet run -c Release -- unpack --out ./out --dest ./trainset
```

Output layout after unpack:
```
trainset/
  images/   *.png   (64×64 grayscale)
  labels/   *.txt   (YOLO: class cx cy w h, normalized)
out/
  classes.txt       (classId → letter, for YOLO/PaddleOCR)
  shard-0001.tar
  shard-0002.tar
  ...
```

---

## Module map

| File | Role |
|------|------|
| `Model.cs` | Data contracts — `Node` (mutable struct, pool-friendly), `WriterProfile`, `YoloLabel` |
| `Alphabet.cs` | Embedded Hershey 'futural' a–z skeletons (public domain) |
| `Ingestion.cs` | Template parser — Hershey built-in + SVG path/polyline; runs once at startup |
| `Noise.cs` | Deterministic 1D value noise for tremor (zero dependencies) |
| `Kinematics.cs` | Affine → curvature-based velocity → tremor, all in-place on `Span<Node>` |
| `Renderer.cs` | SkiaSharp — variable-width stroke, bbox from stroked path (`GetFillPath` + `TightBounds`) |
| `Imperfections.cs` | Endpoint overshoot/undershoot, lead-in hooks, loop crossover/gap |
| `Pipeline.cs` | Per-worker chain — `ArrayPool` rent/return, per-worker tar shard |
| `Program.cs` | Orchestrator — load once, `Parallel.ForEach` across samples, two-stage CLI |
| `Unpacker.cs` | Optional post-step — extract shards into a flat `images/` + `labels/` tree |

---

## Key design decisions

- **Trajectory, not outline** — perturbations happen on the centerline as a pen path.
- **Velocity from curvature**, not inter-node distance (uniform resampling makes distance-based speed flat).
- **`WriterProfile` sampled once per sample** so every stroke reads like the same hand.
- **Bounding box covers ink** (padded by stroke width via `GetFillPath`), not the skeleton.
- **Per-worker tar shards** — I/O stays embarrassingly parallel, no lock funnel.
- **`ArrayPool` + `clearArray:false`** — the hot path is allocation-free.
- All Skia objects disposed via `using` (native memory, not GC-managed).

---

## Realism passes

Human error is **structured, biased, and localized** — not uniform noise.

- [x] Endpoint overshoot / undershoot
- [x] Lead-in / lead-out hooks
- [x] Loop crossover / gap (biased per-writer toward crossing over)
- [ ] Proportion / length jitter per segment
- [ ] Vertical bounce + slight per-letter rotation
- [ ] Speed-scaled two-band tremor (smooth fast, shaky slow)
- [ ] Ink dynamics (pooling at slow points, occasional pen-skip gaps)
- [ ] Allographic variation (multiple skeletons per letter)

---

## Dependencies

- [SkiaSharp](https://github.com/mono/SkiaSharp) — rendering
- .NET 8+

---

## License

MIT
