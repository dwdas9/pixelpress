# Current State

Class C — overwritten in full at every checkpoint. Not a narrative.

Last updated: 2026-07-10 (M10 slice 2 written and committed, NOT yet verified)

## Current Milestone

M10 — Compression studio UI (in progress; Core seam done, studio UI
written and committed but never run with an image in it).

## Last Completed Action

M10 slice 2 landed as commit `a2d07c6` — the studio UI consuming
`IPreviewEncoder`:

- **`MainWindowViewModel`** takes `IPreviewEncoder` (DI already
  registered it; no wiring change needed). `PlanItemRow` was widened to
  `(SourcePath, FileName, FormatSummary, SizeText, OutputFormat,
  SourceBytes, EstimatedOutputBytes)` so a selection alone can build a
  `PreviewRequest`. `SelectedPlanItem` defaults to the first row and
  survives a re-plan by `SourcePath`. `RefreshPreviewAsync` encodes on a
  background thread, cancels the in-flight encode on each change, and
  falls back to the heuristic estimate when `Encode` returns null.
  Exposes `OriginalPreviewImage` / `OutputPreviewImage` bitmaps,
  `PreviewSavingsText`, `PreviewDimensionsText`, `ZoomFactor`.
  The old advanced-panel toggle is gone — the rail surfaces everything.
- **`MainWindow.axaml`** state 3 is now the two-pane studio: preview
  area (side-by-side `ORIGINAL`|`OPTIMIZED` + zoom slider + filmstrip)
  ‖ controls rail (quality, convert-to, resize, metadata, output, plus
  a deliberately empty `PER-CODEC SETTINGS` slot per ADR-0006 §5)
  ‖ a compression-statistics strip. Window is 1180×800, min 980×660.
- **`AppStyles.axaml`** gained the preview backdrop colour (both theme
  dictionaries) and the `pane` / `paneLabel` / `railHeading` /
  `reservedSlot` / `statLarge` / `filmstrip` classes.

ADR-0006's separation is enforced in the UI: `THIS IMAGE` reads
`Exact — this image, encoded` with no tilde; `WHOLE BATCH` reads
`Estimated` with a `~`. The two numbers may legitimately disagree.

Three latent crash paths were found and fixed while writing this slice,
all in the fire-and-forget preview path where an escaped exception kills
the process: (1) `Task.Run(fn, token)` *faults* with
`TaskCanceledException` when the token is cancelled before the pool
picks the work up — which is exactly what a fast slider drag produces —
so the driver is split into a wrapper that catches
`OperationCanceledException` plus `RefreshPreviewCoreAsync`; (2) both
bitmap swaps now publish-then-dispose, because the renderer may still
hold the old bitmap for the current frame and disposing it is a crash,
not a leak; (3) `TryDecodeBitmap` catches `Exception`, not a fixed list
— the platform decoder's exception type for AVIF/JXL/HEIC/RAW is not a
contract we control.

Also designed around, not shipped as a bug: the quality-aware
`SizeEstimator` changes each row's `EstimatedOutputBytes`, breaking
`PlanItemRow` record equality, which would fire
`OnSelectedPlanItemChanged` on every re-plan *and* `ReplanAsync`'s own
refresh — two encodes per slider tick. An `_isApplyingPlan` guard makes
`ReplanAsync` the single trigger point. Don't remove it.

## Current Blockers

**M10 slice 2 has never been run with an image loaded.** This is the one
thing standing between here and the M10 close-out.

Verified without a human: solution builds with 0 warnings; 59/59 tests
pass; the studio XAML parses at runtime; the window comes up at the new
1180×800 (read back via UI Automation `BoundingRectangle`); the app
starts and exits cleanly (exit 0, empty stderr) on two separate runs.

Not verified — everything downstream of a file drop, because a
smoke-launch never gets there. Driving the native "Choose images" dialog
via UI Automation was attempted and abandoned: `ValuePattern.SetValue`
times out on the only reachable Edit (the search box),
`SetForegroundWindow` is a no-op from a background process, and the
dialog exposes no `File name:` field to UIA. That road spends tokens
verifying Microsoft's shell dialog, not the studio. It needs a human.

## Next Immediate Task

Run `dotnet run --project src/PixelPress.Desktop`, drag in an image, and
walk this checklist. The first two are the ones most likely to be wrong:

1. **Both panes show the picture**, each fitting its pane — not cropped,
   not blown up to actual pixel size. Each `Image` is sized to its
   *pane's* `Bounds` (`{Binding #OriginalPane.Bounds.Width}`) so that
   zoom 1.0 means "fit". Binding to the `ScrollViewer`'s viewport
   instead would loop as scrollbars appear and disappear. This binding
   could still misbehave on first layout.
2. **The quality slider re-encodes.** Drag it in the rail; after a short
   pause the `OPTIMIZED` pane and the `THIS IMAGE` numbers change.
3. Zoom slider (1×–4×) scales both panes; "Fit" returns to 1.0.
4. The filmstrip switches the selected image and the preview follows.
5. With resize on, `PreviewDimensionsText` reads `3200 × 2100 → 1024 × 672`.
6. Convert to AVIF or JPEG XL: the `OPTIMIZED` pane shows the
   "previews aren't supported on this system" note while the numbers
   beside it stay exact. (Skia cannot decode these; the encoder still can.)
7. `THIS IMAGE` says `Exact — this image, encoded` (no tilde);
   `WHOLE BATCH` says `Estimated` (with `~`).
8. Dark mode looks right in the new layout.

A 3200×2100 test JPEG (556,726 bytes) was generated for this at
`%LOCALAPPDATA%\Temp\claude\c--Users-dasd-OneDrive---xg5qw-Github-pixelpress\6d4dff27-5ddf-4206-9998-2e9449b42f56\scratchpad\studio-test.jpg`
— it may have been cleaned up; any photo will do.

Fix whatever the checklist turns up. **Then, and only then**, the M10
close-out: `docs/ARCHITECTURE.md` M10 row `In progress` → `Done`, a
`RELEASES.md` entry, this file rewritten, commit. After that, M11
(packaging) is the last milestone.

## Files worth reading before the close-out (in addition to the index below)

- src/PixelPress.Core/Processing/IPreviewEncoder.cs (the consumed seam)
- RELEASES.md (for the entry's house style)

## Context Dependency Index

- decisions/0006-lossy-quality-and-live-feedback.md
- docs/ARCHITECTURE.md
- src/PixelPress.Core/Processing/IImageCodec.cs
- src/PixelPress.Core/Processing/MagickImageCodec.cs
- src/PixelPress.Core/Formats/ImageFormat.cs
- src/PixelPress.Desktop/ViewModels/MainWindowViewModel.cs
- src/PixelPress.Desktop/Views/MainWindow.axaml
- src/PixelPress.Desktop/Views/MainWindow.axaml.cs
- src/PixelPress.Desktop/Styles/AppStyles.axaml
