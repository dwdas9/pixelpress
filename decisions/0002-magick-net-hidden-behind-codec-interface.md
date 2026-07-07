# ADR-0002: Magick.NET is hidden behind IImageCodec, Q8 build

Status: Accepted

## Context

Magick.NET is the only realistic choice for the encode/decode matrix
PixelPress needs to support (JPEG, PNG, WebP, AVIF, HEIC/HEIF, TIFF,
BMP, GIF, JPEG XL, ICO, camera RAW). Coupling the engine's public API
to it directly would make it impossible to swap later, and its Q16
build costs roughly double the memory of Q8 for no visible benefit on
consumer photos.

## Decision

Magick.NET types never appear outside `Core/Processing/`. All access
goes through `IImageCodec`, and Core exposes its own `ImageFormatId`
instead of `MagickFormat`. The bundled build is Q8, not Q16. Worker
concurrency is capped and ImageMagick resource limits are set at
startup so a pathological input degrades gracefully instead of
exhausting RAM.

## Consequences

The processing library can be replaced without touching the public
contract or any caller in Desktop. Costs one layer of adapter code in
`MagickImageCodec`. Q8 means no perceptible quality loss for the
targeted use case, but would need revisiting if PixelPress ever needs
to preserve >8-bit-per-channel precision.
