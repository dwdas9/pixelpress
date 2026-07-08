# ADR-0005: Settings persistence and resize scope

Status: Accepted

## Context

M8 needs sticky user preferences (an advanced panel: format override,
resize, strip metadata, overwrite originals) and, for resize
specifically, a genuinely new capability — nothing in the engine
touches pixel dimensions today. ADR-0003 fixed PixelPress as
lossless-only with no user-facing quality/compression dial, and flagged
that ruling anything like it in later would need a new ADR, not a UI
tweak.

## Decision

1. Settings persist as JSON at
   `Environment.SpecialFolder.ApplicationData/PixelPress/settings.json`
   (the platform-appropriate app-data folder). A missing or corrupt
   file silently falls back to defaults — it never blocks startup.
2. Resize is modeled as an independent dimension:
   `JobRequest.ResizeEnabled` / `ResizeMaxDimensionPixels`. It is a
   pixel-count reduction, not a compression-quality trade-off, so it
   does not reopen ADR-0003 — the preset/quality system is unchanged.
3. The plan-time size estimate (`SizeEstimator`) does **not** account
   for resize savings. The Plan/Execute split's "no pixel work during
   planning" invariant (`docs/ARCHITECTURE.md`, Engine design) means the
   planner never opens an image to read its dimensions; estimating
   resize savings would require exactly that. The plan preview instead
   captions that real savings may exceed the estimate when resize is
   on. This is a documented limitation, not a deferred bug.

## Consequences

`Settings/` is new in `PixelPress.Core`. `JobRequest` and the internal
`CodecRequest` each grow two fields. `MagickImageCodec` gains a resize
step between metadata stripping and quality. `SizeEstimator` is
unchanged by design — a future milestone that wants resize-aware
estimates would need to either probe image headers during planning (a
performance and architecture trade-off) or accept an approximate
heuristic, and that decision belongs to its own ADR.
