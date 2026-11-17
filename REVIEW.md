# Review checklist

Read the diff against each line that applies. Most rules here come from `CLAUDE.md`, `Swof.Engine.Tests/AGENTS.md` and the `swof-*` skills, gathered by what the diff touches. A rule that does not hold is a finding, not a style note.

## Any change

- `tools/check.sh fast` passes on the final tree, not on an earlier one.
- The report says what was not exercised. Nothing here proves motion, tempo or touch feel; a person has to play it.

## `Swof.Engine/`

- Game logic stays here; hosts only read input, step the engine and draw.
- A new number that changes play lives in `PhysicsTuning`, not inline.
- New behaviour has a scenario in `Swof.Engine.Tests/Program.cs` that fails without the change.
- Anything that maps screen to court goes through `snapshot.AsSeen()` / `snapshot.FromScreen(input)`; the simulation never swaps ends.
- A gameplay change: run `tools/check.sh matches` and `tools/check.sh matches 100 1 equal`, and say which seeds moved with `tools/check.sh diff main`.
- A change meant not to alter play (refactor, rename, a cleanup): `tools/check.sh diff main` shows no difference.

## `Swof.Game/`, `Swof.Engine/MatchFlow.cs`, `Swof.Engine/PauseMenu.cs`, `Swof.Engine/TouchControls.cs`

- `tools/check.sh full` builds the host.
- A visible change has a frame taken with `--capture` (and `--script` for anything reached by input) and the PNG was opened, not just written.
- `Swof.Game/*.cs` also compiles into `Swof.Droid`; code that must differ on a phone asks `OperatingSystem.IsAndroid()`. Only CI builds Android.
- `tools/check.sh diff main` shows no difference unless the change is meant to alter play.

## `Swof.Windows/`

- No new features in its renderer; it is kept for the tuning grid.

## The gates themselves (`Swof.Engine.Tests/`, `tools/`, `.github/workflows/`)

- A deleted or weakened scenario, or a moved pass mark in `MatchRunner.FailedMarks`, has a written reason in the commit or PR. "It was flaky" is not one: the engine is deterministic, so find what changed.
- No skip, retry, sleep or widened tolerance added to get a failing scenario to pass.
- A changed gate was shown to fail on a deliberately broken tree, not only to pass on a good one.
- A change to the per-seed `OK`/`FAIL` line format in `MatchRunner` breaks `tools/check.sh diff`; update both.

## Docs and skills

- `CLAUDE.md` keeps implemented behaviour apart from decided design; "What does not exist yet" is updated when something moves across.
- Commands written into docs were run, as written, in this change.
