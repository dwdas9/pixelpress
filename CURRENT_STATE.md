# Current State

Class C — overwritten in full at every checkpoint. Not a narrative.

Last updated: 2026-07-09 (M9 close-out / M10 kickoff checkpoint)

## Current Milestone

M10 — Compression studio UI (not started).

## Last Completed Action

Closed out M9 (lossy quality control). Shipped in commits `35a65b7`
(ADR-0006 + doc restructure) and `32e0e12` (implementation): `Quality`
(1–100) is now the single lossy dial through the whole engine, applied
only to formats flagged `ImageFormat.HasQualityDial`
(JPEG/WebP/AVIF/JPEG XL); the fixed-preset model is deleted; the
`SizeEstimator` is quality-aware; a live quality slider replaced the
preset cards on the drop screen and in the plan-preview header, with a
debounced re-plan so batch totals (size/%/bytes-saved) update as it
moves. Build clean, 55 tests pass, app smoke-launched without XAML
errors. Marked the M9 row Done and added the M9 RELEASES entry.

## Current Blockers

None.

## Next Immediate Task

Start M10 — the premium two-pane "compression studio" redesign
(ADR-0006 §4–§5). This is the second half of the product owner's
request. Build order suggestion:

1. **`IPreviewEncoder` seam in Core** (ADR-0006 §4): encode ONE image at
   a given quality/format/resize to an in-memory buffer, returning exact
   output bytes + output dimensions (and source dimensions). Read-only;
   neither planning nor execution. Add Core tests with a fake.
2. **Studio layout** in `MainWindow.axaml`: two-pane workspace — preview
   area (left; side-by-side original|output with a zoom control) +
   controls rail (right; quality slider, format, resize, metadata,
   output — with clearly reserved space for a future lossy-codec
   section) + a compression-statistics strip (orig→output, %, saved,
   dimension change). Reserve room so future lossy controls drop in
   without re-layout.
3. **View-model**: a "selected image" for the studio preview, driving a
   debounced `IPreviewEncoder` call; expose real output size, real
   dimensions, and the preview bitmaps. Show "12.4 MB → 3.1 MB
   (75% smaller)" style stats that update on every setting change.

Verification caveat: M10 is GUI-heavy and hard to verify headlessly —
smoke-launch after each slice (as done in M9) and commit green
increments frequently given the token-budget risk the user flagged.

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
