# Current State

Class C — overwritten in full at every checkpoint. Not a narrative.

Last updated: 2026-07-09 (M8 close-out checkpoint)

## Current Milestone

M9 — Packaging (not started).

## Last Completed Action

Closed out M8 (settings persistence + advanced panel). The
implementation shipped in commit `0651a7e` (committed by the user
between sessions, before this checkpoint was written — which is why
the previous checkpoint still said "not started"): `Settings/` in
Core (`AppSettings`, `ISettingsStore`/`JsonSettingsStore` writing JSON
to the OS app-data folder), advanced panel in the plan preview (format
override, resize-to-max-dimension, strip metadata, overwrite
originals), resize plumbed through `JobRequest` → `JobExecutor` →
`CodecRequest` → `MagickImageCodec` (`Less = true`, never upscales),
settings loaded in the view-model constructor and saved on shutdown in
`App.axaml.cs`, plus ADR-0005 and tests (54 passing). This session
added the ADR-0005 §3 plan-preview caption ("actual savings may exceed
this estimate" when resize is on), flipped the M8 milestone row to
Done, and wrote the M8 RELEASES entry.

## Current Blockers

None.

## Next Immediate Task

Plan M9 (Stage 1 — human decides, session proposes): confirm M9 scope
before touching code. Known ingredients: self-contained publish for
win-x64 / osx-arm64 / osx-x64, app icon + vector logo (deferred from
M7), final polish. Open call for the user: do the M7-deferred live
per-file queue statuses (needs engine per-item events) go into M9 or
get dropped? Record the outcome as the M9 row in the milestone table,
then start.

## Context Dependency Index

- docs/ARCHITECTURE.md
- RELEASES.md
- Directory.Build.props
- src/PixelPress.Desktop/PixelPress.Desktop.csproj
- README.md
