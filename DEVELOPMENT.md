# Development

Pure index. This file asserts no facts of its own — if it and
`docs/ARCHITECTURE.md` ever disagree, `docs/ARCHITECTURE.md` wins.

## Prerequisites

.NET 8 SDK.

## Commands

    dotnet build
    dotnet test
    dotnet run --project src/PixelPress.Desktop
    dotnet run --project src/PixelPress.Desktop -- --verify-codecs

## Where things are

| Question | Look at |
|---|---|
| Where does memory live for this project? | `MASTER_PROTOCOL.md`, `docs/ARCHITECTURE.md`, `decisions/`, `CURRENT_STATE.md`, `RELEASES.md` — see below |
| What am I working on right now? | `CURRENT_STATE.md` |
| What does the system look like, and what's the milestone plan? | `docs/ARCHITECTURE.md` |
| Why was a hard call made a particular way? | `decisions/NNNN-*.md` |
| What's shipped so far? | `RELEASES.md`, then `git log` |
| What are the non-negotiable rules for this codebase? | `MASTER_PROTOCOL.md` |
| How does a session start / end? | `SESSION_START.md` / `SESSION_END.md` |
