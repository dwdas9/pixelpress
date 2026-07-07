# PixelPress architecture

Author: D Das

## Shape

Single-process desktop app. Two assemblies plus tests:

- **PixelPress.Core** — the engine. Formats, presets, job contracts,
  planner, executor. No UI references, ever. Magick.NET lives inside
  `Processing/` behind an internal codec interface; no ImageMagick type
  appears in the public API.
- **PixelPress.Desktop** — Avalonia UI, MVVM (CommunityToolkit.Mvvm),
  DI composition root in `Infrastructure/ServiceCollectionExtensions`.
  References Core. Never touches Magick.NET directly.

## Dependency rules

1. Core references no UI assembly.
2. Desktop references Core, not Magick.NET.
3. Core exposes its own format identities (`ImageFormatId`), not
   `MagickFormat`, so the processing library can be swapped without
   breaking the contract.

## Engine design (M2/M3)

Two-phase pipeline:

- **Plan** — scan dropped paths, classify files via `FormatRegistry`,
  resolve output paths and naming conflicts, estimate savings. No pixel
  work. Produces a complete pre-flight plan the UI shows before anything
  runs.
- **Execute** — bounded worker pool (physical core count), one file per
  task. Writes go to a temp file, atomically renamed on success; originals
  are never touched until the replacement is verified. Failures never stop
  the batch; they are collected into the end-of-run summary. Cancellation
  is honored between files.

## Format capabilities

`FormatRegistry` is the single source of truth for decode/encode support.
HEIC, ICO and camera RAW are decode-only. The UI's output picker is
generated from `FormatRegistry.EncodableFormats`, so the UI cannot offer
a conversion the engine cannot perform.

## Memory

Magick.NET Q8 build (half the memory of Q16, imperceptible for consumer
photos). Worker concurrency is capped and ImageMagick resource limits are
set at engine startup so a pathological input degrades gracefully instead
of exhausting RAM.

## M4 notes

- PNG compression-level tuning (per-preset) is deferred — Magick.NET's
  define-based API for it needs on-machine confirmation before relying
  on a guessed signature. Default PNG compression is used for now.
- `MagickImageCodec` and `CodecVerifier.cs` are the only files in the
  engine that could not be compiled in the sandbox this project was
  built in (no network access to nuget.org for the Magick.NET package).
  Everything else — including the executor's orchestration logic — was
  compiled and test-executed against the real source. Run
  `dotnet run --project src/PixelPress.Desktop -- --verify-codecs` on a
  real machine to confirm the codec matrix; see README.
- Animated GIF/WebP sources are decoded via `MagickImageCollection` with
  `Coalesce()`, not `MagickImage` directly — the latter silently reads
  only the first frame of a multi-frame file.

## NuGet security auditing

.NET 8's built-in NuGet Audit checks every restored package against a
known-vulnerability database and reports matches as build warnings
(NU1901-1904). Under `TreatWarningsAsErrors` these become build failures.

Two things keep this from being either a silent risk or a recurring
build-breaker:

- Direct dependencies (Magick.NET in particular, since it bundles many
  third-party delegate libraries such as libpng and libwebp) are kept on
  a current release, since maintainers patch reported vulnerabilities in
  later versions. Bump on `dotnet restore` warnings, not just on a fixed
  schedule.
- `Directory.Build.props` sets `WarningsNotAsErrors` for the audit codes
  specifically (NU1901-1904), per Microsoft's documented guidance for
  projects using `TreatWarningsAsErrors`. This keeps vulnerabilities
  *visible* in build output without letting a newly-discovered issue in
  a transitive dependency silently brick the entire build until patched.
  Genuine code-quality and correctness warnings are still hard errors.
