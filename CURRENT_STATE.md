# Current State

Class C — overwritten in full at every checkpoint. Not a narrative.

Last updated: 2026-07-09 (M10 mid-milestone checkpoint — slice 1 done)

## Current Milestone

M10 — Compression studio UI (in progress; Core seam done, UI not started).

## Last Completed Action

M10 slice 1 landed (commits `d192e1e`, `220cfa1`): the read-only
`IPreviewEncoder` seam from ADR-0006 §4. `IPreviewEncoder` /
`PreviewRequest` / `PreviewResult` are public in
`src/PixelPress.Core/Processing/IPreviewEncoder.cs`; the Magick-backed
`MagickPreviewEncoder` encodes one image at a given quality/format/resize
to an in-memory buffer and reports exact output bytes + source/output
dimensions. Exposed via `PreviewEncoding.CreateDefault()`, registered in
DI (`ServiceCollectionExtensions`), NOT yet consumed by the view model.
Shared `ImageFormatId→MagickFormat` mapping extracted to
`MagickFormatMap`. End-to-end encoder tests were added and caught a
real M8 bug: `ApplyResize` used `MagickGeometry.Less` (the `<` flag =
upscale-only), so resize never shrank large images — fixed to `Greater`
(`>`) in both the codec and the preview encoder. Build clean, 59 tests
pass. (Earlier this session: ADR-0006 + M9 quality slider, both already
closed out — see RELEASES.)

## Current Blockers

None. Note a verification constraint (not a blocker): the studio preview
path only runs after a file is dropped, which a headless smoke-launch
does not exercise, so the remaining UI slice should be built and
**visually verified in a real run** rather than shipped blind.

## Next Immediate Task

M10 slice 2 — the studio UI, consuming `IPreviewEncoder`. Inject
`IPreviewEncoder` into `MainWindowViewModel` and:

1. **View-model**: pick a "selected image" from the current plan
   (default the first item), and on selection/quality/format/resize
   change, debounced, call `IPreviewEncoder.Encode` on a background
   thread (`await Task.Run(...)`; assign results after the await so the
   property set lands on the UI thread). Expose the real output size,
   real source/output dimensions, a "12.4 MB → 3.1 MB (75% smaller)"
   stat string, and before/after `Avalonia.Media.Imaging.Bitmap`s
   (construct the Bitmap from `PreviewResult.OutputImage` bytes; dispose
   the previous one on replace). Fall back to the heuristic estimate
   when `Encode` returns null.
2. **Layout** (`MainWindow.axaml`): evolve the plan-preview into the
   two-pane studio — preview area (side-by-side original|output + a zoom
   control, e.g. a `Slider` driving a `LayoutTransform` scale inside a
   `ScrollViewer`) + controls rail (the existing quality slider, format,
   resize, metadata, output — leave a clearly-marked empty slot for the
   future lossy-codec section) + a compression-statistics strip.
3. Smoke-launch, drop a real image, confirm the preview + live stats
   update as the quality slider moves, then commit.

Do the M10 close-out (row → Done, RELEASES entry) only after slice 2 is
verified.

## Files worth reading before slice 2 (in addition to the index below)

- src/PixelPress.Core/Processing/IPreviewEncoder.cs (the seam to consume)

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
