# Current State

Class C — overwritten in full at every checkpoint. Not a narrative.

Last updated: 2026-07-12 (M10 validated on a real batch; M11 workspace
written, builds, smoke-launches — not yet driven with images)

## Current Milestone

M11 — Workspace UI (written and building; awaiting a human pass with
images in it). M10 is **done and validated**.

## The validated result

A real-world batch: **~5 GB of images → ~500 MB, ~90% reduction, quality
acceptable.** This is the first end-to-end run at real batch size and it
retires the blocker that stood at the top of this file for two
checkpoints ("M10 slice 2 has never been run with an image loaded").

What it proves: the plan/execute split, the quality pipeline
(`JobRequest` → `CodecRequest` → codec → `SizeEstimator`), the worker
pool, and the atomic-write path all hold on a real corpus, not just the
single-image test path.

What it does **not** prove, and must not be written down as if it did:

- It is not a guarantee of 90%. One operator, one corpus, one quality
  setting. No UI copy, README line, or marketing claim should quote a
  fixed savings figure.
- It does not validate quality at any particular slider position.
  "Acceptable" was a human judgement, not a metric.
- It is **not** a reason to retune `SizeEstimator`. The heuristic is
  deliberately conservative; real savings landing above it is the
  designed direction of error. Under-promising is safe. Leave the curve
  alone.

## Last Completed Action

M11 — the workspace redesign. Uncommitted at the time of writing.

- **`MainWindow.axaml`** rewritten: three-column workspace (queue |
  viewport | inspector) with `GridSplitter`s, a toolbar, and a status
  bar. `WorkflowStage` now drives *overlays inside the viewport* rather
  than swapping whole screens — the queue and inspector never unmount, so
  a finished batch can be re-tuned and re-run without back-navigation.
  1440×900, min 960×600.
- **`MainWindow.axaml.cs`** keeps the platform-input work (drag-drop,
  pickers, theme, folder launch) and gains the comparison divider. The
  divider stores a **ratio**, not a pixel offset: window resize, splitter
  drag, zoom change and image change all invalidate a pixel position but
  leave a ratio correct, so one `BoundsProperty` handler covers all four.
  Pointer coordinates are taken relative to `CompareHost`, which sits
  *inside* the zoom's `LayoutTransformControl`, so the transform is
  already unwound. It is deliberately not view-model state.
- **`PlanItemRow`** (new file) stops being a record and becomes a cached,
  mutable `ObservableObject` keyed by source path. **This is the
  load-bearing decision.** A re-plan fires on every settled slider tick
  and rewrites a file's numbers, never its identity; rebuilding the rows
  each time would re-decode every thumbnail and forget every status on
  each nudge of the quality dial. `MainWindowViewModel._rowCache` owns
  them and prunes on plan change.
- **`ExecutionProgress`** gains `LastResult`, `CompletedSourceBytes`,
  `Elapsed`. The executor was already producing an `ItemResult` per file
  and discarding it; now it hands it over, which is what finally delivers
  M7's deferred "live per-file queue statuses (needs engine per-item
  events)". ETA extrapolates from **bytes, not file count**.
- **`EnumMatchConverter`** (new) backs the queue's All/Done/Failed filter
  chips. Its `ConvertBack` returns `BindingOperations.DoNothing` on
  uncheck: radio-group unchecking would otherwise clobber the value the
  new selection just wrote, and the event order is not ours to control.
- **`AppStyles.axaml`** gains the `RadioButton.chip` class.

Two ADR-0006 invariants survived the redesign intact: the `_isApplyingPlan`
guard (single trigger point for the preview encode — **do not remove**),
and the exact-vs-estimated separation (the viewport overlay reads
`Exact — this image, encoded`; the inspector's batch panel reads
`Estimated` with a `~`).

## Rejected, on purpose

A mockup was proposed adding: semantic preset cards, a CPU/GPU load
monitor, per-image histograms, a "diff loupe" with a deviation
percentage, a tiled multi-format comparison, a "Real-Time Savings vs.
Quality" curve with a diminishing-returns marker, and file-manager bulk
actions (rename/move/delete).

Declined, and the reasons are durable:

- **Presets are retired by ADR-0006 §2.** Reintroducing them needs a new
  ADR, not a XAML file.
- **The savings-vs-quality curve is either 10+ encodes per selected image
  per settings change, or it is a picture of the heuristic.** Drawn from
  `SizeEstimator` it would present a guess with the confidence of a
  measurement — exactly what ADR-0006 forbids.
- **There is no GPU work.** Magick.NET encodes on the CPU. A GPU load bar
  would be an animation.
- **Bulk delete/move/rename** would make an offline optimizer that never
  touches files it wasn't given into a file manager that does.

The honest half of that mockup — thumbnails, per-file status, ETA,
throughput, failure filters — is what M11 shipped.

## Current Blockers

None hard. M11 builds with 0 warnings, 61/61 tests pass (2 new, covering
`ExecutionProgress.LastResult` and the byte accumulation), and the app
launches and stays up with empty stderr.

Not verified: anything downstream of a file drop **in the new layout**.
The 5 GB validation run exercised the engine, which is shared, but the
workspace chrome around it has not been driven by a human.

## Next Immediate Task

Run `dotnet run --project src/PixelPress.Desktop`, drop in a folder, and
walk this list — it is short because the engine is now trusted and only
the new chrome is suspect:

1. **Thumbnails appear and fill in progressively** without stalling the
   queue. AVIF/RAW rows should keep the format badge permanently — that
   is correct, not a bug.
2. **The comparison divider drags**, and stays put across a window
   resize, a splitter drag, and a zoom change.
3. **Per-file ticks and crosses land during a run**, and the status bar
   shows a plausible ETA and throughput.
4. The All / Done / Failed filter chips filter, and survive a re-run.
5. `LayoutTransformControl` inside the `ScrollViewer` behaves under zoom
   (this pairing is the least-tested thing in the file).
6. Dark mode across the new columns.

Then commit, and M12 (packaging) is the last milestone.

## Files worth reading before the next session

- src/PixelPress.Desktop/ViewModels/PlanItemRow.cs (the row cache contract)
- src/PixelPress.Core/Execution/ExecutionModels.cs (the widened progress record)

## Context Dependency Index

- decisions/0006-lossy-quality-and-live-feedback.md
- docs/ARCHITECTURE.md
- src/PixelPress.Core/Execution/ExecutionModels.cs
- src/PixelPress.Core/Execution/JobExecutor.cs
- src/PixelPress.Core/Processing/IPreviewEncoder.cs
- src/PixelPress.Desktop/ViewModels/MainWindowViewModel.cs
- src/PixelPress.Desktop/ViewModels/PlanItemRow.cs
- src/PixelPress.Desktop/ViewModels/EnumMatchConverter.cs
- src/PixelPress.Desktop/Views/MainWindow.axaml
- src/PixelPress.Desktop/Views/MainWindow.axaml.cs
- src/PixelPress.Desktop/Styles/AppStyles.axaml
