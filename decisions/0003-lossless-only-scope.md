# ADR-0003: Lossless-only scope

Status: Accepted

## Context

Adding a lossy-quality slider (JPEG/WebP/AVIF quality knobs) was on
the table during the M6 UI redesign. It would touch the plan/estimate
pipeline, the preset model, and the UI in ways that are hard to walk
back once users depend on them.

## Decision

PixelPress stays lossless-only: it optimizes and converts formats
without a user-facing quality/compression trade-off control. Reaffirmed
explicitly during M6 with no functional changes made toward lossy
support.

## Consequences

Keeps the planner's size estimates deterministic and the preset model
simple (a preset is a target format + container options, not a quality
curve). Ruling this in later is a new ADR, not a UI tweak — it would
touch `Presets`, `SizeEstimator`, and the plan preview contract.
