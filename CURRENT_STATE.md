# Current State

Class C — overwritten in full at every checkpoint. Not a narrative.

Last updated: 2026-07-07

## Current Milestone

M8 — Settings persistence + advanced panel (not started).

## Last Completed Action

M7 (Premium UI) shipped: light/dark theming via theme dictionaries and
`DynamicResource`, header + status bar structure, file table in the
plan preview, empty-state copy, micro-transitions. Deferred out of M7:
live per-file queue statuses (needs engine per-item events — likely an
M8/M9 concern) and a vector logo (packaging milestone, M9).

## Current Blockers

None.

## Next Immediate Task

Design the settings persistence model for M8: where settings live on
disk, what the advanced panel exposes (format override, resize, strip
metadata, overwrite originals), and how it plugs into the existing
preset/job pipeline without breaking the lossless-only scope
(ADR-0003).

## Context Dependency Index

- docs/ARCHITECTURE.md
- decisions/0003-lossless-only-scope.md
- src/PixelPress.Core/Presets/Presets.cs
- src/PixelPress.Core/Jobs/JobRequest.cs
- src/PixelPress.Desktop/Infrastructure/ServiceCollectionExtensions.cs
- src/PixelPress.Desktop/ViewModels/MainWindowViewModel.cs
