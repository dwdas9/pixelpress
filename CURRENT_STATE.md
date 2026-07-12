# Current State

Class C — overwritten in full at every checkpoint. Not a narrative.

Last updated: 2026-07-12 (M11a: never-inflate rule + menus/zoom/truncation;
engine fix is tested, the new UI chrome is not yet clicked)

## Current Milestone

M11a — done and committed on branch `m11-workspace-ui`, not yet merged to
`main`. M10 and M11 are done. M12 (packaging) is the last milestone.

## The two results that matter, read together

- **~5 GB → ~500 MB (~90% smaller), quality acceptable.** A real batch.
- **615 MB → 886 MB (+44%).** Also a real batch, at "Near-original"
  quality, on the same engine.

Same app, opposite outcomes. **The result is dominated by the quality
setting, not by the application.** Quote either figure without the other
and you will mislead someone, including yourself. The second one produced
ADR-0007.

## Last Completed Action

Fixed the inflation bug and three UI issues. Commit on `m11-workspace-ui`.

**ADR-0007 — the never-inflate rule.** A run never returns a bigger file
than it was given, for any file whose only requested change was
compression. `InflationGuard` (new, in Core/Processing) discards an encode
that came out no smaller and keeps the original, as
`ItemOutcome.KeptOriginal` — neither success nor failure.

The root cause was **not** a codec defect. Re-encoding an
already-compressed JPEG at quality 91–100 *legitimately* produces a larger
file: the encoder spends bits preserving the previous encode's artifacts.
ImageMagick did as it was told. Nothing in PixelPress ever compared the
result against the source, so the pipeline had no concept of an encode not
worth keeping.

**The guard's scope is the load-bearing part. It stands down whenever the
user asked for more than smaller bytes:**

- format conversion → the user wants WebP; returning the PNG ignores that
- a resize that actually changed dimensions → they wanted a dimension cap
- **metadata stripping → a privacy request. Keeping the original to save
  bytes would silently retain the GPS data the user asked to remove. Never
  trade that. A larger, stripped file is correct.**

(A resize that was *enabled* but changed nothing, because the source was
already under the cap, is still a pure re-compression — the guard fires.
That is why `CodecResult.WasResized` reports what actually happened rather
than what was requested.)

`SizeEstimator` was lying in the same direction: a linear quality curve
predicted a 22% saving at quality 100 on a re-encode that in reality grows.
The curve is now steeper above 80, and same-format estimates are clamped to
at most the source size. `MagickPreviewEncoder` applies the identical
guard, so the preview cannot promise a saving the run will decline.

**The three UI issues:**

- **Wheel zoom** over the viewport, handled so the ScrollViewer does not
  also scroll. Steps are multiplicative — a fixed ±0.25 crawls at 4× and
  lurches at 0.25×. Wheel, menu and buttons all route through the view
  model's `NudgeZoom`/`ZoomIn`/`ZoomOut` so they cannot drift apart.
- **Menu bar + context menus** (queue and viewport). Every item is bound to
  a real command — a menu entry that never lights up is worse than none.
  New: remove-from-queue (re-plans from the remaining sources rather than
  patching the plan), reveal-in-file-manager, zoom commands, filter
  shortcuts. Right-click selects the row under the cursor first.
- **Summary truncation** was structural, not a sizing problem:
  `FormatSavings` returned whole sentences ("roughly the same size (some
  formats need re-encoding)") into a 36px stat slot. It now returns a
  figure only; explanations get their own wrapped line. Widening the
  control would only have moved the clipping.

## Current Blockers

None hard. Builds clean; **69/69 tests pass** (8 new: `InflationGuardTests`
covers all four stand-down cases, plus an executor test asserting the exact
615→886 scenario keeps the original, lands it in the output folder, and
leaves no temp file). The app launches and stays up with empty stderr.

**The old 886 MB output folder is still bad output.** That batch needs
re-running.

## Next Immediate Task

Human pass. The engine fix is tested; the chrome is not. Nobody has yet
*clicked* any of this:

1. Wheel zoom over the preview — does it feel right, does the pane stay put.
2. Menu bar and both context menus; right-click a queue row and remove it.
3. Per-file status: ✓ optimized, `=` unchanged (kept), `!` failed.
4. Run a JPEG batch at Near-original and confirm the summary now says
   "No change" with the kept-originals note, and the output folder is not
   bigger than the input.
5. Thumbnails filling in; AVIF/RAW rows correctly keep the format badge.
6. Divider survives resize / splitter drag / zoom. Dark mode.

Then merge to `main` (`git merge --ff-only m11-workspace-ui`) and start M12.

## Files worth reading before the next session

- decisions/0007-never-inflate.md (the guarantee and its exceptions)
- src/PixelPress.Core/Processing/InflationGuard.cs (the one implementation)

## Context Dependency Index

- decisions/0006-lossy-quality-and-live-feedback.md
- decisions/0007-never-inflate.md
- docs/ARCHITECTURE.md
- src/PixelPress.Core/Processing/InflationGuard.cs
- src/PixelPress.Core/Processing/MagickPreviewEncoder.cs
- src/PixelPress.Core/Planning/SizeEstimator.cs
- src/PixelPress.Core/Execution/JobExecutor.cs
- src/PixelPress.Core/Execution/ExecutionModels.cs
- src/PixelPress.Desktop/ViewModels/MainWindowViewModel.cs
- src/PixelPress.Desktop/ViewModels/PlanItemRow.cs
- src/PixelPress.Desktop/Views/MainWindow.axaml
- src/PixelPress.Desktop/Views/MainWindow.axaml.cs
