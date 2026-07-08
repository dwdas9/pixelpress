# Current State

Class C — overwritten in full at every checkpoint. Not a narrative.

Last updated: 2026-07-09 (M9 kickoff checkpoint)

## Current Milestone

M9 — Lossy quality control (in progress).

## Last Completed Action

Recorded the scope reversal the product owner requested: replace fixed
presets with a quality slider, show real-time size feedback, redesign
the UI as a premium two-pane "compression studio," and reserve room for
a future lossy-codec mode. Wrote ADR-0006 (supersedes ADR-0003, whose
status line now points to it), split the work into M9 (engine + slider +
live batch feedback) and M10 (studio UI redesign + live per-image
preview) and pushed Packaging to M11 in the ARCHITECTURE milestone
table, and added the "Quality and live feedback" section to
ARCHITECTURE. No code changed in this checkpoint commit.

## Current Blockers

None.

## Next Immediate Task

Implement the M9 engine slice, then commit it green:
1. Add `int Quality { get; init; } = 80;` to `JobRequest` and
   `CodecRequest`; remove `Preset`/`OptimizationPreset` from both.
2. `MagickImageCodec.ApplyQuality` applies `Quality` to lossy formats
   (JPEG/WebP/AVIF/JPEG XL) only; PNG/BMP unchanged.
3. Replace `SizeEstimator.PresetFactor` with a continuous
   `QualityFactor(quality, output)` curve; `JobPlanner` passes
   `request.Quality`; validate 1–100.
4. `JobExecutor`: drop `Presets.Get`, pass `request.Quality` through.
5. Delete `Presets.cs` (`PresetId`, `OptimizationPreset`, `Presets`).
6. UI: replace the three preset cards with a quality slider bound to a
   `Quality` VM property (debounced re-plan so batch totals update live);
   `AppSettings` persists `Quality` instead of `Preset`.
7. Update Core tests that reference presets (JobExecutorTests
   "Codec_receives_the_preset...", any FakeImageCodec preset use) to
   assert on `Quality`. `dotnet test` must be green before commit.

Then M10 (studio UI + `IPreviewEncoder`) is the following milestone —
do NOT start it until M9 is committed.

## Context Dependency Index

- decisions/0006-lossy-quality-and-live-feedback.md
- docs/ARCHITECTURE.md
- src/PixelPress.Core/Jobs/JobRequest.cs
- src/PixelPress.Core/Presets/Presets.cs
- src/PixelPress.Core/Planning/SizeEstimator.cs
- src/PixelPress.Core/Planning/JobPlanner.cs
- src/PixelPress.Core/Processing/IImageCodec.cs
- src/PixelPress.Core/Processing/MagickImageCodec.cs
- src/PixelPress.Core/Execution/JobExecutor.cs
- src/PixelPress.Core/Settings/AppSettings.cs
- src/PixelPress.Desktop/ViewModels/MainWindowViewModel.cs
- src/PixelPress.Desktop/Views/MainWindow.axaml
- tests/PixelPress.Core.Tests/Execution/JobExecutorTests.cs
- tests/PixelPress.Core.Tests/Planning/JobPlannerTests.cs
