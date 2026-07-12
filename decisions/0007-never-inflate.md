# ADR-0007: An optimizer never hands back a bigger file

Status: Accepted

Relates to: ADR-0006 (lossy quality and live feedback).

## Context

A real batch run at "Near-original" quality took **615 MB of JPEGs and
produced 886 MB** — a 44% *increase* — while the completion summary
cheerfully reported "99 images optimized".

This was not a codec defect. Re-encoding an already-compressed image at a
high quality setting legitimately produces a larger file: the encoder
spends bits faithfully preserving the compression artifacts of the
previous encode, on top of the image itself. ImageMagick did exactly what
it was asked. The defect was that nothing in PixelPress ever compared the
encoder's output against the source, so the pipeline had no concept of an
encode that was not worth keeping.

ADR-0006 made quality a user-facing dial spanning 1–100. The top of that
range is precisely where this happens, so the dial as shipped had a region
that reliably made things worse.

## Decision

1. **The output of a run is never larger than its input, for any file
   whose only requested change was compression.** When an encode comes out
   no smaller than the source, the encode is discarded and the original is
   kept. This is a product guarantee, not a heuristic — it is the minimum
   an application called an *optimizer* owes its user.

2. **The rule is scoped to a pure re-compression, and stands down whenever
   the user asked for something else.** It does not fire when:

   - **the output format differs from the source.** A PNG → WebP
     conversion is a transformation the user requested. Silently handing
     back the PNG because the WebP came out larger would ignore the
     request.
   - **a resize actually changed the image's dimensions.** Resize is asked
     for to satisfy a dimension cap, not to save bytes. Returning the
     original would return the wrong-sized image. (A resize that was
     *enabled* but changed nothing — because the source was already under
     the cap — is still a pure re-compression, and the rule does fire.)
   - **metadata stripping was requested.** This is the important one.
     Stripping is normally a *privacy* action — EXIF, GPS coordinates.
     Keeping the original to save bytes would silently retain the location
     data the user explicitly asked to remove. **Bytes are never worth
     trading for that.** A larger, stripped file is the correct outcome.

3. **`KeptOriginal` is a first-class outcome, not a failure and not a
   success.** The file is fine; there was simply nothing to gain. It is
   reported honestly — in the queue, the summary, and the status bar —
   because a user who sees "optimized" next to an unchanged file has been
   lied to, and a user who sees "failed" will go looking for a bug.

4. **The planner and the live preview apply the same rule.** The estimate
   may not promise a saving the executor will decline, and the preview must
   show the file the user will actually receive. One shared
   `InflationGuard`, consumed by the executor and the preview encoder.

## Consequences

- `SizeEstimator`'s quality curve is steeper above 80. Output size does not
  rise linearly with the quality setting, and a linear curve predicted a
  comfortable saving at quality 100 for a re-encode that in reality grows.
  Same-format estimates are additionally clamped to at most the source
  size, since the guard makes growth impossible.
- `IFileSystem` gains `CopyFile`: when writing to a separate output folder,
  a kept original still has to reach that folder, or the batch would
  silently be missing a file.
- The completion summary reports `ProcessedCount` (succeeded + kept) rather
  than `SucceededCount`, plus a plain-language note when files were kept,
  so "no change" reads as a deliberate decision instead of a silent no-op.
- A user who genuinely wants a bigger file (a deliberate upscale, a
  quality-raising re-encode) cannot get one from a same-format run. That is
  the intended trade: this application optimizes, and if a better tool for
  inflating images is wanted, it is not this one.
- The heuristic estimate and the real encode may still disagree for one
  image (ADR-0006), and that remains fine. What may no longer happen is the
  *batch* getting bigger.
