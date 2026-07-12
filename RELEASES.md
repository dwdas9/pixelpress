# Releases

Class D, curated — one terse paragraph per shipped milestone, indexing
commit history. Never a substitute for `git log`; never mid-milestone.

> **Note (2026-07-12):** history was rewritten to strip `Co-Authored-By`
> trailers, so every SHA after the root commit changed. The hashes below
> are the current ones. Any SHA quoted in an older chat log or issue is
> dead — match by commit subject instead.

## M1–M7 (2026-07-07, `fe1ced3`)

All of M1 through M7 currently live in a single commit (`fe1ced3`,
"Commit by das") — the repository was imported/scaffolded in one shot
rather than committed milestone-by-milestone. Together they shipped:
solution scaffold, format registry, presets, and Avalonia shell (M1);
the planner — path scanning, classification, conflict resolution (M2);
drag-and-drop input and plan preview UI (M3); the Magick.NET-backed
executor with atomic writes and cancellation (M4); the executor wired
live into the plan preview (M5); the first design-system pass (M6);
and light/dark theming plus the premium UI polish (M7). See
`docs/ARCHITECTURE.md`'s milestone table for scope detail and
`decisions/` for the irreversible calls made along the way.

## M8 (2026-07-09, `c3170c7` + close-out commit)

Settings persistence and the advanced panel. The plan preview gains an
"Advanced options" card: convert-to format override, resize-to-fit (a
pixel-count cap that never upscales — deliberately independent of the
lossless preset system, ADR-0005), EXIF/metadata stripping, and
overwrite-originals with an in-place warning. The panel's values plus
the chosen preset persist as JSON in the OS app-data folder
(`JsonSettingsStore`), loaded at startup and saved at shutdown; a
missing or corrupt file falls back to defaults without blocking
launch. The size estimate intentionally ignores resize savings —
planning never opens pixels — and the preview captions that actual
savings may exceed it.

## M9 (2026-07-09, `da57faf` + `51d0e31`)

Lossy quality control — the reversal of ADR-0003 (recorded as ADR-0006).
A single `Quality` dial (1–100) replaces the three fixed presets and
travels the whole engine: `JobRequest` → `CodecRequest` →
`MagickImageCodec` (applied only to formats flagged
`ImageFormat.HasQualityDial` — JPEG/WebP/AVIF/JPEG XL) and into a new
quality-aware `SizeEstimator` curve. The preset model
(`PresetId`/`OptimizationPreset`/`Presets`) is deleted. In the UI a
quality slider takes the preset cards' place on the drop screen and adds
a second slider to the plan-preview header; moving it re-estimates the
whole-job size/%/bytes-saved live (debounced so a drag re-plans once it
settles). Settings persist `Quality` instead of the preset.

## M10 (2026-07-12, `a977bde` + `ede9759` + `45043c4` + `6d0bf45`)

The compression studio. Real per-image numbers arrive: `IPreviewEncoder`
(ADR-0006 §4) encodes the selected image at the chosen quality into an
in-memory buffer — a third, read-only concern that is neither planning
(no pixels) nor execution (writes files) — and the view model drives it
debounced, cancelling the in-flight encode on every change. The window
becomes a two-pane studio: side-by-side `ORIGINAL`|`OPTIMIZED` panes
with zoom and a filmstrip, a controls rail, and a statistics strip that
keeps ADR-0006's separation visible on screen (`Exact — this image,
encoded`, no tilde, against a `~`-prefixed batch estimate). Three latent
crash paths in the fire-and-forget preview path were found and fixed
while writing it: `Task.Run(fn, token)` faults rather than returns when
a fast slider drag cancels before the pool picks the work up; bitmap
swaps must publish-then-dispose because the renderer may still hold the
old bitmap for the current frame; and `TryDecodeBitmap` catches
`Exception`, since the platform decoder's failure type for AVIF/JXL/
HEIC/RAW is not a contract we control.

## M11 (2026-07-12, `dc7a93e`)

The workspace. The studio's two panes become a persistent three-column
layout — queue | viewport | inspector, with a toolbar and status bar —
and `WorkflowStage` stops swapping whole screens: the drop zone, the
planning state and the completion summary are now overlays inside the
viewport, so the inspector stays reachable at every stage and a finished
batch can be re-tuned and re-run without back-navigation. The queue gains
what M7 deferred as "needs engine per-item events": `ExecutionProgress`
now carries the `ItemResult` it was already producing and discarding,
plus `CompletedSourceBytes` and `Elapsed`, which gives per-file status
ticks in the queue and a **byte-based** ETA and throughput in the status
bar (a 40 MB raw among ninety-nine thumbnails is not "1% done" when the
first file lands). `PlanItemRow` stops being a record and becomes a
cached, mutable row keyed by source path — a re-plan fires on every
settled slider tick and changes a file's *numbers*, never its identity,
so rebuilding the rows would re-decode every thumbnail and forget every
status on each nudge of the quality dial. Thumbnails decode via
`Bitmap.DecodeToWidth`, so a 6000×4000 source never materialises 24M
pixels; formats with no platform decoder keep a format badge for good.

Shipped alongside the project's first real-world validation: ~5 GB of
images reduced to ~500 MB (~90%) at acceptable quality. See
`docs/ARCHITECTURE.md` for what that does and does not prove.

## M11a (2026-07-12, `a274cf0` + `a4df8d0` + `69a01c0`)

**The never-inflate rule (ADR-0007), and the bug that forced it.** A real
batch at "Near-original" quality turned 615 MB of JPEGs into 886 MB while
the summary reported "99 images optimized". Not a codec defect —
re-encoding an already-compressed image at high quality genuinely produces
a larger file, because the encoder spends bits preserving the previous
encode's artifacts. The defect was that nothing ever compared the output
against the source. `InflationGuard` now discards any encode that came out
no smaller and keeps the original (`ItemOutcome.KeptOriginal`: not a
success, not a failure, reported honestly in the queue and the summary).
It deliberately stands down when the user asked for more than smaller
bytes — a format conversion, a resize that actually changed dimensions, or
metadata stripping, which is a privacy request and is never traded for
bytes. The executor and the preview encoder share the one implementation,
so the preview cannot promise a saving the run will decline, and
`SizeEstimator`'s quality curve steepens above 80 because output size does
not rise linearly with the dial.

Alongside it: mouse-wheel zoom over the viewport (multiplicative steps, so
it feels the same at 0.25× and 4×), a menu bar and queue/viewport context
menus wired to real commands only, remove-from-queue, reveal-in-file-
manager, and a fix for the clipped summary label — which was clipped
because `FormatSavings` returned whole sentences into a 36px stat slot, so
the stat now returns a figure and the sentence gets its own line.

Then (`69a01c0`) the batch estimate was made genuinely live. It had been
routed through a full `CreatePlan` on every quality change, which re-walked
the file system and re-resolved every naming conflict in order to redo a
multiplication — and was gated on the `PlanReady` stage, so with the
workspace's always-visible inspector it appeared frozen entirely.
`JobPlanner.ReEstimate` is pure arithmetic over an existing plan: quality
cannot change a plan's *shape* (not the file set, not the formats, not the
output paths, not which names collide), only one number per item. No
"refresh estimate" button was needed — the expensive work simply should not
have been happening. Zoom is now anchored to the pointer (the pixel under
the cursor stays under the cursor), left-drag pans, and the comparison
divider is grabbed by its handle alone — it had been sitting under a
transparent full-surface Canvas that swallowed every press, which is what
made panning impossible.
