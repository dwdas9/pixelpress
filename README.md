# PixelPress

Offline image optimizer and format converter for Windows and macOS.
Drop images or folders, pick a quality preset, done.

Author: D Das

## Status

Milestone 1 — solution scaffold, format registry, presets, app shell.
See `docs/ROADMAP.md` for the plan.

## Build

Requires .NET 8 SDK.

    dotnet build
    dotnet test
    dotnet run --project src/PixelPress.Desktop

## Format support

Input: JPEG, PNG, WebP, AVIF, HEIC/HEIF, TIFF, BMP, GIF, JPEG XL, ICO,
camera RAW (DNG, CR2, CR3, NEF, ARW, ORF, RW2, RAF).

Output: JPEG, PNG, WebP, AVIF, TIFF, BMP, GIF, JPEG XL.

HEIC, ICO and RAW are input-only. HEIC encoding is not offered because
the bundled codecs do not include an HEVC encoder (patent-encumbered);
convert HEIC to AVIF or JPEG instead.

## Layout

    src/PixelPress.Core       engine: formats, presets, planning, processing (no UI)
    src/PixelPress.Desktop    Avalonia UI (MVVM)
    tests/                    engine tests (xunit)
    docs/                     architecture and roadmap

## Verifying codecs

Image codec behaviour is the one part of this app that can only be
confirmed on a real machine. Run:

    dotnet run --project src/PixelPress.Desktop -- --verify-codecs

This round-trips a small generated test image through every encodable
format and reports pass/fail per format. Run it once per platform
(Windows and macOS) after pulling a milestone that touches the
processing engine.
