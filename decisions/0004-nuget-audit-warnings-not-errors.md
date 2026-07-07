# ADR-0004: NuGet security-audit codes excluded from TreatWarningsAsErrors

Status: Accepted

## Context

.NET 8's built-in NuGet Audit checks every restored package against a
known-vulnerability database and reports matches as build warnings
(NU1901-1904). The project builds with `TreatWarningsAsErrors`. Left
unhandled, a newly-discovered vulnerability in any dependency —
including a transitive one bundled inside Magick.NET, such as libpng
or libwebp — would silently break every build the moment the advisory
database updates, with no code change on our side.

## Decision

`Directory.Build.props` sets `WarningsNotAsErrors` for NU1901-1904
specifically, per Microsoft's documented guidance for projects using
`TreatWarningsAsErrors`. Every other warning, including genuine
code-quality and correctness ones, stays a hard error.

## Consequences

A newly-flagged vulnerability shows up in build output instead of
silently blocking work, but doesn't force an emergency dependency bump.
The trade is that these four codes must be watched deliberately (bump
Magick.NET on `dotnet restore` warnings, not just on a fixed schedule)
rather than relying on the build to force the issue.
