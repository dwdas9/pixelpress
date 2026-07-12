# Current State

Class C — overwritten in full at every checkpoint. Not a narrative.

Last updated: 2026-07-12, end of session. Everything is committed, merged
to `main`, and pushed. Working tree clean.

## Where you are

**M11a is done and on `main`.** M1–M11a all shipped. **M12 (packaging) is
the only milestone left.**

Nothing is half-finished. There is no branch to merge, no stash, no
in-flight refactor. You can start tomorrow cold.

## Read this before you touch anything

**History was rewritten on 2026-07-12.** Every `Co-Authored-By: Claude`
trailer was stripped from all 19 commits and `main` was force-pushed;
the merged `m11-workspace-ui` branch was deleted from GitHub. GitHub now
lists one contributor.

Consequences:

- **Every SHA except the root `fe1ced3` changed.** Any hash in an old chat
  log, issue, or note is dead. `RELEASES.md` has been remapped to the
  current hashes — trust it, not your memory.
- A local tag `backup/pre-trailer-strip` still points at the old
  pre-rewrite history (`e5a8078`). Delete it once you are confident:
  `git tag -d backup/pre-trailer-strip`
- A local branch `m11-workspace-ui` still exists (rewritten, clean, fully
  merged). Safe to delete: `git branch -D m11-workspace-ui`
- The old commits still exist on GitHub's servers, reachable by SHA via
  PR #1's retained head ref. Only GitHub Support can purge those. It does
  not affect the contributors list.

**Never add a `Co-Authored-By:` trailer to a commit in this repo.** The
user is the sole contributor by explicit instruction.

## The three results that define this project

- **~5 GB → ~500 MB (~90% smaller)**, quality acceptable. A real batch.
- **615 MB → 886 MB (+44%)**, at "Near-original" quality. Also real, same
  engine. This produced ADR-0007.
- **69→71 tests green**, build clean, app launches clean.

The first two are the point: **the outcome is dominated by the quality
setting, not by the application.** Quote either figure alone and you
mislead someone, including yourself.

## What shipped today

- **M11 (`dc7a93e`)** — the three-column workspace (queue | viewport |
  inspector). `WorkflowStage` stopped swapping whole screens; the drop
  zone, planning state and summary are now overlays *inside* the viewport,
  so the inspector stays reachable at every stage. Queue rows gained
  thumbnails and per-file status; `ExecutionProgress` gained `LastResult`,
  `CompletedSourceBytes` and `Elapsed`, giving byte-based ETA/throughput.
- **M11a (`a274cf0`)** — **ADR-0007, the never-inflate rule.** An encode
  that comes out no smaller is discarded and the original kept
  (`ItemOutcome.KeptOriginal`). It stands down whenever the user asked for
  more than smaller bytes: a format conversion, a resize that actually
  changed dimensions, or **metadata stripping — a privacy request, never
  traded for bytes.** `InflationGuard` is shared by the executor and the
  preview encoder so the preview cannot promise a saving the run declines.
- **`a4df8d0`** — startup crash: `InputGesture="Ctrl+Plus"` is not a valid
  Avalonia `Key`; it is `OemPlus`. An unparseable gesture anywhere in the
  tree kills the whole window at construction.
- **`69a01c0`** — live batch estimate (`JobPlanner.ReEstimate`, pure
  arithmetic, no I/O), cursor-anchored zoom, drag-to-pan, divider grabbed
  by its handle only.

## Invariants — do not "simplify" these away

1. **`_isApplyingPlan`** in `MainWindowViewModel` is the single trigger
   point for the preview encode. Removing it gives two encodes per slider
   tick.
2. **`PlanItemRow` is cached and mutable, keyed by source path.** A re-plan
   fires on every settled slider tick and changes a file's *numbers*, never
   its identity. Rebuilding the rows re-decodes every thumbnail and forgets
   every status.
3. **The comparison divider is view state, not view-model state.** It is a
   ratio against a control's bounds; it must not survive a re-plan.
4. **`SizeEstimator` is deliberately conservative.** Real savings landing
   above the estimate is the designed direction of error. **Do not retune
   it toward the observed 90%.** Under-promising is safe; over-promising is
   not.
5. **Only quality may take the cheap `ReEstimate` path.** Every other
   setting can change the plan's shape (file set, formats, output paths,
   name collisions) and needs a full `CreatePlan`.

## Rejected on purpose — do not re-litigate

From a proposed mockup: semantic preset cards (**retired by ADR-0006 §2;
needs a new ADR, not a XAML file**), CPU/GPU load monitors (**there is no
GPU work — Magick.NET encodes on the CPU; a GPU bar would be an
animation**), a "Real-Time Savings vs. Quality" curve with a
diminishing-returns marker (**either 10+ encodes per image per settings
change, or a picture of the heuristic presented with the confidence of a
measurement**), per-image histograms, a diff loupe, and file-manager bulk
actions (**rename/move/delete would turn an offline optimizer that never
touches files it wasn't given into a file manager that does**).

The honest half of that mockup — thumbnails, per-file status, ETA,
throughput, failure filters — is what M11 shipped.

## Next session — pick one

**1. The human pass nobody has done.** The engine is well tested; the
chrome has never been clicked. Run it, drop a folder, and check: wheel zoom
feel and pointer anchoring; drag-to-pan; the divider handle surviving
resize/zoom; per-file ✓ / `=` / `!` marks; thumbnails filling in (AVIF and
RAW correctly keep the format badge — that is not a bug); menus and both
context menus; dark mode.

**Re-run a JPEG batch at Near-original** and confirm the summary now reads
"No change" with the kept-originals note, and the output folder is *not*
bigger than the input. **The old 886 MB output folder is still bad output.**

**2. M12 — packaging.** Self-contained publish for win-x64 / osx-arm64 /
osx-x64, icon, final polish. Mostly mechanical.

**3. Known loose end, not yet filed.** Re-planning after a run, into the
same output folder, sees the previous run's outputs already on disk and
resolves the "collision" by renaming — so a second pass can produce
`photo (2).jpg`. Nobody has hit it yet. It wants a decision about whether
re-running into a folder should overwrite its own prior output.

## Files worth reading before the next session

- decisions/0007-never-inflate.md (the guarantee and its three exceptions)
- src/PixelPress.Core/Processing/InflationGuard.cs

## Context Dependency Index

- decisions/0006-lossy-quality-and-live-feedback.md
- decisions/0007-never-inflate.md
- docs/ARCHITECTURE.md
- src/PixelPress.Core/Planning/JobPlanner.cs
- src/PixelPress.Core/Planning/SizeEstimator.cs
- src/PixelPress.Core/Processing/InflationGuard.cs
- src/PixelPress.Core/Execution/JobExecutor.cs
- src/PixelPress.Core/Execution/ExecutionModels.cs
- src/PixelPress.Desktop/ViewModels/MainWindowViewModel.cs
- src/PixelPress.Desktop/ViewModels/PlanItemRow.cs
- src/PixelPress.Desktop/Views/MainWindow.axaml
- src/PixelPress.Desktop/Views/MainWindow.axaml.cs
