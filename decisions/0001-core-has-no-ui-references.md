# ADR-0001: Core engine has zero UI references

Status: Accepted

## Context

PixelPress needs its image-processing engine to be testable in
isolation and, longer-term, reusable from something other than the
Avalonia desktop shell (a CLI, a second UI, etc.).

## Decision

`PixelPress.Core` never references any UI assembly. `PixelPress.Desktop`
references Core and is the only assembly allowed to reference Avalonia.
The dependency arrow points one way, always.

## Consequences

Makes the engine unit-testable with plain xunit, no UI test harness
needed. Costs a small amount of indirection (DI composition root lives
in `Desktop/Infrastructure`, not colocated with the types it wires up).
