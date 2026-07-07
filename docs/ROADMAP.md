# Roadmap

Each milestone compiles, runs, and is production-quality for what it
contains. No placeholder code.

Reordered after M2: input handling and the plan preview moved ahead of
the executor, so there is something interactive sooner. The executor
still comes before execution is wired up live, in the same shape it
always would have.

- [x] **M1** Scaffold. Solution, Directory.Build.props, format registry +
      capability matrix, presets, Avalonia shell with DI, engine tests.
- [x] **M2** Planner. Job contracts, path scanning, file classification,
      nested folders, conflict resolution, plan summary. Pure logic, tested.
- [x] **M3** Input + plan preview (UI, thin). Drag-and-drop, file/folder
      pickers, plan preview screen (count, size estimate, output folder,
      fallback/rename/skip callouts). Optimize button present but disabled
      — no executor to call yet.
- [x] **M4** Executor. Magick.NET adapter, worker pool, atomic writes,
      metadata preservation, cancellation, per-file results.
- [x] **M5** Wire the executor into the plan preview screen: Optimize
      button goes live, progress, cancel, completion summary, calm error list.
- [x] **M6** UI redesign. Design system (palette, typography, cards,
      button hierarchy), all five states restyled, drag-over feedback.
      Lossless-only scope reaffirmed; no functional changes.
- [x] **M7** Premium UI. Light/dark theming (theme dictionaries +
      DynamicResource), header + status bar structure, file table in the
      plan preview, empty-state copy, micro-transitions. Deferred: live
      per-file queue statuses (needs engine per-item events), vector logo
      (packaging milestone).
- [ ] **M8** Settings persistence + advanced panel (format override, resize,
      strip metadata, overwrite originals).
- [ ] **M9** Packaging. Self-contained publish for win-x64 / osx-arm64 /
      osx-x64, icon, final polish.
