# PixelPress Strategic Focus

**Date**: 2026-07-14  
**Decision**: After auditing against Squoosh, PixelPress will NOT chase codec-tuning features. Instead, focus on batch-first workflow.

---

## Positioning

**PixelPress**: Batch-first optimizer for photographers and content creators.  
**Squoosh**: Codec-tuning lab for enthusiasts.

We own the workflow Squoosh doesn't have.

---

## What We're NOT Building

- Per-codec parameters (quantization algorithms, progressive JPEG toggles, chroma subsampling, etc.)
- Advanced compression knobs (grayscale conversion, PNG compression level, smoothing)
- Codec selection UI beyond "keep original format" or "convert to X"

**Why**: Each adds complexity (UI, tests, docs, support) without serving our core user ("drop 200 images, make them smaller, done").

---

## What We ARE Building

### 1. Performance
- [ ] Parallel encoding (today: single-threaded)
- [ ] Memory efficiency for large batches (100+ MB batches)
- [ ] Progress indication + ETA accuracy

**Metric**: Encode 1 GB of images in < 60 seconds on mid-range hardware.

### 2. Workflow
- [ ] Drag multiple folders at once (today: one at a time)
- [ ] Save/load compression profiles ("for web", "for archive", etc.)
- [ ] Scheduled/recurring batch runs
- [ ] Exclusion patterns (ignore `.*`, `thumbs`, etc.)

**Metric**: Zero re-planning on format/quality changes; instant preset switching.

### 3. Correctness
- [ ] Per-image breakdown: "saved N% because resize", "saved M% because quality"
- [ ] Confidence intervals on estimates (not just "~58% smaller")
- [ ] Warn if quality slider won't actually help (e.g., downsampled image)
- [ ] Validate codec support before planning (don't promise WebP to IE11 users)

**Metric**: Batch estimate within 5% of actual result; zero "why is this bigger?" surprises.

### 4. Ease of Use
- [ ] Remember window state, output folder history
- [ ] Keyboard shortcuts for common tasks (clear queue, optimize, open folder)
- [ ] Drag-to-reorder queue
- [ ] Right-click context menu for batch operations
- [ ] Explain why a file was skipped/renamed (inline tooltips)

**Metric**: New user can drop a folder and ship optimized images in < 2 minutes.

### 5. UI Polish
- [ ] Consistent icon styling
- [ ] Accessibility: ARIA labels, keyboard navigation, high-contrast mode
- [ ] Tooltip consistency (when to show, when to hide)
- [ ] Animation smoothness (queue updates, progress bar)
- [ ] Dark/light theme completeness

**Metric**: No theme-specific bugs; all controls keyboard-navigable.

---

## Non-Goals

- Codec-specific tuning
- Image editing (crop, rotate, adjust, filter)
- Cloud upload/sync
- Batch scheduling via CLI (out of scope for GUI app)
- Plugin system for codecs

These are not "nice-to-haves deferred"; they are *explicitly out of scope* to stay focused.

---

## Decision Log

**Codec Tuning Features Rejected** (2026-07-14):
- Progressive JPEG toggle
- PNG compression level slider
- Grayscale conversion
- Per-codec advanced panels

**Rationale**: Squoosh owns codec tuning. PixelPress owns batch workflow. Don't blur the line.

**Reference**: FEATURE_AUDIT.md (gap analysis vs. Squoosh)

---

## Quarterly Milestones (Proposed)

### Q3 2026
- [ ] Parallel encoding (2–3x speedup)
- [ ] Profile save/load
- [ ] Per-image savings breakdown

### Q4 2026
- [ ] Drag multiple folders
- [ ] Exclusion patterns
- [ ] UI polish pass (icons, tooltips, a11y)

### Q1 2027
- [ ] Scheduled batch runs
- [ ] Confidence intervals on estimates
- [ ] Performance benchmarking dashboard

---

## Conversation Reference

**Audit**: User questioned if PixelPress should add progressive JPEG, PNG compression, grayscale.  
**Assessment**: No. Stay focused on batch workflow, not codec tuning.  
**Conclusion**: Redirect effort to performance, workflow, correctness, UX polish.
