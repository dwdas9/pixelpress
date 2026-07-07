# Session-End Protocol

Its whole job is to leave `CURRENT_STATE.md` in a state that lets the
next, entirely memory-less session resume with zero reconstruction.
Overwrite that one file completely, in its fixed schema. Touch nothing
else.

Run this:

- At genuine session end.
- Periodically mid-session too — roughly every few substantial
  exchanges, or after finishing a coherent chunk of work — without
  waiting to be asked. Sessions end abruptly more often than they end
  cleanly (context limit, crash, dropped connection); a checkpoint
  written recently loses only a few minutes of work if the session
  dies right now.

If a genuinely irreversible decision was made this session, write its
ADR in `decisions/` before or alongside this checkpoint — never
retrofitted later from memory.

Before finishing, skim the diff to `CURRENT_STATE.md`. It's the only
artifact carrying context into the next session — worth ten seconds of
review.
