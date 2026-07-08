# Releases

Class D, curated — one terse paragraph per shipped milestone, indexing
commit history. Never a substitute for `git log`; never mid-milestone.

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

## M8 (2026-07-09, `0651a7e` + close-out commit)

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

From M9 onward, each milestone close-out adds its own dated entry
here, anchored to the commit(s) that shipped it.
