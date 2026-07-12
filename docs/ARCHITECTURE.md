# PixelPress architecture

Author: D Das

## Milestones

Class B1 — the only roadmap this project keeps; a table rewritten in
place, never a growing prose document. Reordered after M2: input
handling and the plan preview moved ahead of the executor, so there
was something interactive sooner. See `CURRENT_STATE.md` for which
one is active right now and what's next inside it.

| # | Milestone | Status | Scope |
|---|---|---|---|
| M1 | Scaffold | Done | Solution, `Directory.Build.props`, format registry + capability matrix, presets, Avalonia shell with DI, engine tests. |
| M2 | Planner | Done | Job contracts, path scanning, file classification, nested folders, conflict resolution, plan summary. Pure logic, tested. |
| M3 | Input + plan preview | Done | Drag-and-drop, file/folder pickers, plan preview screen (count, size estimate, output folder, fallback/rename/skip callouts). Optimize button present but disabled — no executor yet. |
| M4 | Executor | Done | Magick.NET adapter, worker pool, atomic writes, metadata preservation, cancellation, per-file results. |
| M5 | Wire executor into plan preview | Done | Optimize button goes live: progress, cancel, completion summary, calm error list. |
| M6 | UI redesign | Done | Design system (palette, typography, cards, button hierarchy), all five states restyled, drag-over feedback. Lossless-only scope reaffirmed; no functional changes. |
| M7 | Premium UI | Done | Light/dark theming (theme dictionaries + `DynamicResource`), header + status bar structure, file table in the plan preview, empty-state copy, micro-transitions. Deferred: live per-file queue statuses (needs engine per-item events), vector logo (packaging milestone). |
| M8 | Settings persistence + advanced panel | Done | Advanced panel in the plan preview: format override, resize to a max dimension (pixel-count cap, never upscales — independent of the quality system, ADR-0005), strip metadata, overwrite originals. All of it plus the quality choice persists as JSON in the OS app-data folder, loaded at startup, saved at shutdown. Size estimate deliberately ignores resize savings (no pixel work at plan time); the preview captions this. |
| M9 | Lossy quality control | Done | Reverses ADR-0003 (see ADR-0006). `Quality` (1–100) is the single lossy dial through `JobRequest`/`CodecRequest`/`SizeEstimator`/codec (applied only to formats with `ImageFormat.HasQualityDial`); the three fixed presets are deleted. A live slider replaces the preset cards — on the drop screen and in the plan-preview header — and moving it re-estimates the batch totals live (debounced). |
| M10 | Compression studio UI | Done | Two-pane premium redesign: large preview area (side-by-side original\|output, zoom), controls rail (quality, format, resize, metadata, output; space reserved for a future lossy-codec section), and a compression-statistics strip. Real per-image output size + dimensions via a live preview encode behind a new `IPreviewEncoder` seam (ADR-0006 §4). |
| M11 | Workspace UI | Done | The studio's two panes become a persistent three-column workspace (queue \| viewport \| inspector) with a toolbar and status bar. Workflow stages become overlays *inside* the viewport rather than whole screens, so the inspector stays reachable at every stage and a finished batch can be re-run without back-navigation. Queue rows gain thumbnails and per-file status; `ExecutionProgress` gains `LastResult`/`CompletedSourceBytes`/`Elapsed`, giving byte-based ETA and throughput. |
| M12 | Packaging | Not started | Self-contained publish for win-x64 / osx-arm64 / osx-x64, icon, final polish. |

## Shape

Single-process desktop app. Two assemblies plus tests:

- **PixelPress.Core** — the engine. Formats, job contracts, quality
  model, planner, executor, preview encoder, settings store. No UI
  references, ever. Magick.NET lives inside
  `Processing/` behind an internal codec interface; no ImageMagick type
  appears in the public API.
- **PixelPress.Desktop** — Avalonia UI, MVVM (CommunityToolkit.Mvvm),
  DI composition root in `Infrastructure/ServiceCollectionExtensions`.
  References Core. Never touches Magick.NET directly.

## Dependency rules

1. Core references no UI assembly.
2. Desktop references Core, not Magick.NET.
3. Core exposes its own format identities (`ImageFormatId`), not
   `MagickFormat`, so the processing library can be swapped without
   breaking the contract.

## Engine design (M2/M3)

Two-phase pipeline:

- **Plan** — scan dropped paths, classify files via `FormatRegistry`,
  resolve output paths and naming conflicts, estimate savings. No pixel
  work. Produces a complete pre-flight plan the UI shows before anything
  runs.
- **Execute** — bounded worker pool (physical core count), one file per
  task. Writes go to a temp file, atomically renamed on success; originals
  are never touched until the replacement is verified. Failures never stop
  the batch; they are collected into the end-of-run summary. Cancellation
  is honored between files.

## Quality and live feedback (M9/M10, ADR-0006)

`Quality` (1–100) is the single lossy dial. It travels `JobRequest` →
`CodecRequest` → `MagickImageCodec` (applied to lossy targets only) and
into `SizeEstimator`. There is no preset model — a slider with labelled
snap points is pure UI.

Three distinct size concerns, deliberately kept separate:

- **Plan-time batch estimate** (`SizeEstimator`): coarse, conservative
  heuristic over `Quality`; no pixel work; drives the whole-job
  "12.4 MB → 3.1 MB (75% smaller)" readout, live as the slider moves.
- **Live per-image preview** (M10, `IPreviewEncoder`): actually encodes
  one selected image at the chosen quality to an in-memory buffer for
  the exact output size, exact output dimensions, and the side-by-side
  preview. A read-only third concern — neither planning nor execution —
  debounced from the view model.
- **Post-run truth** (`ExecutionSummary`): the real bytes written.

The batch estimate and the preview encode can disagree for one image;
that is intended. The UI never conflates them.

### Validated result (2026-07-12)

First real-world batch: **~5 GB of images → ~500 MB, a ~90% reduction at
acceptable quality.** This is the end-to-end validation ADR-0006's lossy
reversal was betting on, and it retires the standing "never run with a
real batch" blocker.

Two things it confirms, and one it does not:

- The plan/execute split and the quality pipeline hold at real batch
  size, not just on the single-image test path.
- `SizeEstimator` is doing its job by being *conservative*. Real savings
  landing well above the heuristic is the designed direction of error —
  under-promising is safe, over-promising is not. Do not "correct" the
  curve toward the observed 90% on one sample.
- It does **not** validate quality at any particular slider position.
  "Acceptable" was one operator's judgement on one corpus at whatever
  quality that run used. It is not a guarantee, and nothing in the UI
  should claim a fixed savings figure.

### Workspace layout (M11)

A persistent three-column workspace — queue (left) | viewport (centre) |
inspector (right), with a toolbar above and a status bar below. Replaces
M10's two-pane studio.

The important change is that `WorkflowStage` no longer swaps whole
screens: the drop zone, the planning state, and the completion summary
are overlays *inside* the viewport, so the queue and inspector stay
mounted at every stage. A finished batch can be re-tuned and re-run
without navigating backwards.

The inspector's `Expander` sections (Format / Quality / Resize /
Metadata / Output) are where ADR-0006 §5's reserved per-codec section
lands, without a re-layout.

The comparison divider is deliberately **not** view-model state. It is a
ratio measured against a control's bounds, means nothing to the planner
or the encoder, and must not survive a re-plan; it lives in
`MainWindow.axaml.cs`.

## Format capabilities

`FormatRegistry` is the single source of truth for decode/encode support.
HEIC, ICO and camera RAW are decode-only. The UI's output picker is
generated from `FormatRegistry.EncodableFormats`, so the UI cannot offer
a conversion the engine cannot perform.

## Memory

Magick.NET Q8 build (half the memory of Q16, imperceptible for consumer
photos). Worker concurrency is capped and ImageMagick resource limits are
set at engine startup so a pathological input degrades gracefully instead
of exhausting RAM.

## M4 notes

- PNG compression-level tuning is deferred — Magick.NET's
  define-based API for it needs on-machine confirmation before relying
  on a guessed signature. Default PNG compression is used for now.
- `MagickImageCodec` and `CodecVerifier.cs` are the only files in the
  engine that could not be compiled in the sandbox this project was
  built in (no network access to nuget.org for the Magick.NET package).
  Everything else — including the executor's orchestration logic — was
  compiled and test-executed against the real source. Run
  `dotnet run --project src/PixelPress.Desktop -- --verify-codecs` on a
  real machine to confirm the codec matrix; see README.
- Animated GIF/WebP sources are decoded via `MagickImageCollection` with
  `Coalesce()`, not `MagickImage` directly — the latter silently reads
  only the first frame of a multi-frame file.

## NuGet security auditing

.NET 8's built-in NuGet Audit checks every restored package against a
known-vulnerability database and reports matches as build warnings
(NU1901-1904). Under `TreatWarningsAsErrors` these become build failures.

Two things keep this from being either a silent risk or a recurring
build-breaker:

- Direct dependencies (Magick.NET in particular, since it bundles many
  third-party delegate libraries such as libpng and libwebp) are kept on
  a current release, since maintainers patch reported vulnerabilities in
  later versions. Bump on `dotnet restore` warnings, not just on a fixed
  schedule.
- `Directory.Build.props` sets `WarningsNotAsErrors` for the audit codes
  specifically (NU1901-1904), per Microsoft's documented guidance for
  projects using `TreatWarningsAsErrors`. This keeps vulnerabilities
  *visible* in build output without letting a newly-discovered issue in
  a transitive dependency silently brick the entire build until patched.
  Genuine code-quality and correctness warnings are still hard errors.
