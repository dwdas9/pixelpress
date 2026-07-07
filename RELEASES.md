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

From M8 onward, each milestone close-out should add its own dated
entry here, anchored to the commit(s) that shipped it.
