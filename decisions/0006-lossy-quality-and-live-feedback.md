# ADR-0006: Lossy quality control and live feedback

Status: Accepted

Supersedes: ADR-0003 (Lossless-only scope).

## Context

ADR-0003 fixed PixelPress as lossless-only with no user-facing
quality/compression control, and stated that ruling that in later would
require a new ADR touching `Presets`, `SizeEstimator`, and the plan
preview contract. The product owner has now decided to do exactly that:
ship a user-facing **quality slider** with **real-time feedback** (the
estimated output size, percentage reduction, and bytes saved update as
the slider moves), and to redesign the interface as a premium
"compression studio" that reserves room for a future lossy-codec mode
(Squoosh-style per-codec settings) without another redesign.

This is a deliberate reversal, not a tweak, so it gets its own ADR.

## Decision

1. **Quality is a first-class engine dimension.** `JobRequest.Quality`
   and `CodecRequest.Quality` (int, 1–100) are the single lossy quality
   dial, applied uniformly to every lossy target format
   (JPEG/WebP/AVIF/JPEG XL). Lossless outputs (PNG/BMP) ignore it. This
   is one fact in one place — the value the codec uses is the value the
   estimator uses is the value the UI shows.

2. **The three fixed presets are retired.** `PresetId`,
   `OptimizationPreset`, and `Presets` are deleted. A slider with a small
   number of labelled snap points ("Smaller · Balanced · High Quality")
   replaces the three preset cards; the labels are pure UI and map to
   slider positions, carrying no engine state of their own.

3. **`SizeEstimator` becomes quality-aware.** The old per-preset factor
   is replaced by a continuous `QualityFactor(quality, output)` curve.
   It stays a coarse, deliberately conservative heuristic that does *no*
   pixel work — the plan-time "no pixels" invariant (ADR-0005 §3) still
   holds. Batch totals in the plan preview are heuristic and update live
   as the slider moves.

4. **Real per-image numbers come from a live preview encode, not the
   planner.** Showing an actual output size / actual dimensions / a
   side-by-side preview for the selected image requires encoding one
   image at the chosen quality. That is an interactive *preview*
   operation, distinct from both planning (no pixels) and execution
   (writes files). It will live behind its own Core seam
   (`IPreviewEncoder`, encoding to an in-memory buffer) and be driven,
   debounced, from the view model. Introducing it does not violate the
   plan/execute split because it is neither phase — it is a third,
   read-only concern. (Implementation lands in M10; this ADR fixes the
   architecture so M10 does not need to relitigate it.)

5. **The window becomes a two-pane studio** (preview area + controls
   rail + a compression-statistics strip) sized so a future lossy-mode
   section drops into the controls rail without a re-layout. See
   `docs/ARCHITECTURE.md` for the living layout description.

## Consequences

- ADR-0003's guarantees no longer hold: estimates are quality-dependent,
  and the preset model is gone. Any doc or code still asserting
  "lossless-only / no quality dial" is stale and must be corrected on
  sight.
- `JobRequest`/`CodecRequest` swap `Preset` for `Quality`; `JobExecutor`
  stops resolving a preset; `SizeEstimator`, the planner, the codec, the
  settings record, and the view model all move to the quality value.
- The heuristic batch estimate and the exact live-preview encode can
  disagree for a given image; this is expected and intended — the batch
  number is a fast whole-job approximation, the preview number is the
  truth for one image. The UI must not present them as the same thing.
- Retiring presets changes the persisted settings shape; a settings file
  written by the preset-era build falls back to defaults (already the
  documented behaviour for unreadable/outdated settings, ADR-0005 §1).
