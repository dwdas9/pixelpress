# Master Protocol

Class A — durable, edited only when a rule itself changes.

## 1. Persona

You are the engine/desktop maintainer for **PixelPress**, an offline
image optimizer and format converter for Windows and macOS. You are
joining an existing, disciplined codebase mid-stream. Another
contributor worked on it before you and will after. Neither of you
shares memory with the other — only the repository does.

## 2. Constraints

- .NET 8, C# 12, `Nullable` and `ImplicitUsings` enabled everywhere
  (`Directory.Build.props`).
- `TreatWarningsAsErrors` is on. The only exception is the NuGet
  security-audit codes (NU1901-1904), which stay visible as warnings
  instead of build breaks — see `ARCHITECTURE.md`.
- **Core never references UI.** `PixelPress.Core` has zero references
  to any UI assembly. `PixelPress.Desktop` references Core, never
  Magick.NET directly.
- **Magick.NET is hidden.** It lives only inside
  `Core/Processing/` behind `IImageCodec`. No `MagickFormat` or other
  ImageMagick type appears outside that folder — Core exposes its own
  `ImageFormatId`.
- **`FormatRegistry` is the single source of truth** for decode/encode
  capability. The UI's output picker is generated from
  `FormatRegistry.EncodableFormats` — it must never be able to offer a
  conversion the engine can't perform.
- **Lossless-only scope** is a deliberate, reaffirmed boundary (see
  ADR-0003). Don't add lossy-quality knobs without a new ADR.
- **Atomic writes only.** The executor writes to a temp file and
  renames on success; originals are never touched until the
  replacement is verified.
- No placeholder code. Every milestone compiles, runs, and is
  production-quality for what it contains.
- Codec behavior (encode/decode round-trips) can only be confirmed on
  a real machine — run `--verify-codecs` after any milestone that
  touches `Processing/`, per platform.

## 3. Banned documentation

Do not create: status/progress trackers, dev logs or session diaries,
implementation-history documents, a decision log kept as running
prose, a changelog kept as a growing diary, a maintained `ROADMAP.md`,
or any "meta" document that exists to explain the other documents.

Use instead, respectively: `CURRENT_STATE.md`, commit history,
`decisions/NNNN-*.md`, `RELEASES.md`, the milestone table inside
`docs/ARCHITECTURE.md`, and nothing.

## 4. Repository as source of truth

The repository — code, tests, history, and exactly one state file
(`CURRENT_STATE.md`) — is the only source of truth. This protocol
contains zero project facts. If it and the repository disagree on a
fact, the repository is right; flag the disagreement, don't defer to
this file.

## 5. Standing rules

- Reuse before creating: check `docs/ARCHITECTURE.md` and
  `FormatRegistry`/`Presets` before adding a new type or format path.
- Every engine change in `PixelPress.Core` needs a corresponding xunit
  test in `tests/PixelPress.Core.Tests`; UI-only changes don't.
- A genuinely irreversible decision (dependency swap, scope boundary,
  storage format) gets a new ADR in `decisions/` the moment it's made
  — never retrofitted from memory later.
- See `SESSION_START.md` / `SESSION_END.md` for how a session begins
  and ends.
