# Project Instructions for AI Agents

This file provides instructions and context for AI coding agents working on this project.

<!-- BEGIN BEADS INTEGRATION v:1 profile:minimal hash:6cd5cc61 -->
## Beads Issue Tracker

This project uses **bd (beads)** for issue tracking. Run `bd prime` to see full workflow context and commands.

### Quick Reference

```bash
bd ready              # Find available work
bd show <id>          # View issue details
bd update <id> --claim  # Claim work
bd close <id>         # Complete work
```

### Rules

- Use `bd` for ALL task tracking — do NOT use TodoWrite, TaskCreate, or markdown TODO lists
- Run `bd prime` for detailed command reference and session close protocol
- Use `bd remember` for persistent knowledge — do NOT use MEMORY.md files

**Architecture in one line:** issues live in a local Dolt DB; sync uses `refs/dolt/data` on your git remote; `.beads/issues.jsonl` is a passive export. See https://github.com/gastownhall/beads/blob/main/docs/SYNC_CONCEPTS.md for details and anti-patterns.

## Agent Context Profiles

The managed Beads block is task-tracking guidance, not permission to override repository, user, or orchestrator instructions.

- **Conservative (default)**: Use `bd` for task tracking. Do not run git commits, git pushes, or Dolt remote sync unless explicitly asked. At handoff, report changed files, validation, and suggested next commands.
- **Minimal**: Keep tool instruction files as pointers to `bd prime`; use the same conservative git policy unless active instructions say otherwise.
- **Team-maintainer**: Only when the repository explicitly opts in, agents may close beads, run quality gates, commit, and push as part of session close. A current "do not commit" or "do not push" instruction still wins.

## Session Completion

This protocol applies when ending a Beads implementation workflow. It is subordinate to explicit user, repository, and orchestrator instructions.

1. **File issues for remaining work** - Create beads for anything that needs follow-up
2. **Run quality gates** (if code changed) - Tests, linters, builds
3. **Update issue status** - Close finished work, update in-progress items
4. **Handle git/sync by active profile**:
   ```bash
   # Conservative/minimal/default: report status and proposed commands; wait for approval.
   git status

   # Team-maintainer opt-in only, unless current instructions forbid it:
   git pull --rebase
   git push
   git status
   ```
5. **Hand off** - Summarize changes, validation, issue status, and any blocked sync/commit/push step

**Critical rules:**
- Explicit user or orchestrator instructions override this Beads block.
- Do not commit or push without clear authority from the active profile or the current user request.
- If a required sync or push is blocked, stop and report the exact command and error.
<!-- END BEADS INTEGRATION -->


## Build & Test

Requires the .NET 10 SDK (`global.json`). `tools/check.sh` finds it on PATH or through `mise exec dotnet@10`.

```bash
tools/check.sh                 # fast gate: all behavior scenarios + whitespace check
tools/check.sh fast "tackle"   # only scenarios whose name contains "tackle"
tools/check.sh full            # fast + MonoGame host build + dotnet format verification
tools/check.sh matches         # 100 unattended computer-versus-computer matches, about a minute
tools/check.sh matches 1 42    # replay one seed
tools/check.sh diff            # the 100 matches on main and on this working tree, line by line; any difference fails (also: diff <base> <count> [matches options])
tools/check.sh matches 100 1 equal   # every outfielder identical, which adds the balance pass marks
tools/check.sh matches 100 1 equal away=half   # a team waits at halfway (also home=half, =up for markers pushed up, home-up=12 for any distance); a measurement, may miss marks
tools/check.sh matches 100 1 equal away-behind=up away-ahead=half   # a team swaps tactic by score and time (also home-)
tools/check.sh matches 100 1 equal away-coach   # the same swap decided by a coach, with its look and delay (also home-coach)
tools/check.sh matches 100 1 equal away-difficulty=easy   # Away plays as the opponent of that level (easy, normal, hard); away-speed=6 and away-technique=6 set every Away outfielder's speed, or its ball control and tackling, for measuring
tools/check.sh matches 100 1 equal away-habits=runner   # every Away outfielder plays with those habits (runner, passer, neutral; also home-habits=); a measurement, may miss marks, and prints each team's passes and goals
dotnet run --project Swof.Game/Swof.Game.csproj -- --opponent-tactic=half   # play against it (also up; --team-tactic= sets your own team's; --opponent-coach gives the opponent a coach that pushes it up when losing and has it wait when winning late, deciding every ten seconds and obeyed five seconds later)
dotnet run --project Swof.Game/Swof.Game.csproj   # play it: opens on the start screen (Play, Difficulty)
dotnet run --project Swof.Game/Swof.Game.csproj -- --start-paused --capture=/tmp/frame.png   # write the frame drawn after two seconds to a PNG and exit (--start-paused shows the pause menu). --capture and --start-paused open in a match, skipping the start screen; add --start-screen to capture it
dotnet run --project Swof.Game/Swof.Game.csproj -- --capture=/tmp/frame.png --capture-after=3 --script=0.5-0.6:P,1-1.1:0.5x0.62   # play back keys and touches first: from-to:Key, or from-to:XxY as fractions of the screen; touches ending in the same #n are one finger, so two of them make a drag. With a script the real keyboard and mouse are ignored
```

If bare `dotnet` fails (a mise shim with no version set), prefix it: `mise exec dotnet@10 -- dotnet ...`.

- `Swof.Engine.Tests` is an executable scenario runner, not a test-framework project. `dotnet test` builds it without running anything, so never use `dotnet test` as evidence.
- `Swof.sln` includes the WPF host and only builds on Windows. On macOS/Linux build the projects individually.
- CI (`.github/workflows/ci.yml`) runs the fast gate on Linux, builds the full solution on Windows, and builds the Android package on Windows (job `build-android`, which uploads the APK as the artifact `swof-android-apk`).
- `.github/workflows/package.yml` (Package) publishes the desktop game self-contained for `win-x64`, `linux-x64` and `osx-arm64` on demand (`gh workflow run Package --ref main`; not on every push, because the repository is private and macOS minutes bill at ten times the Linux rate), and uploads each as `swof-<rid>-<sha>` (a tar, which keeps the executable bit). The Linux folder is published from the macOS runner because the content build needs Arial. Get them with `gh run download <run id>`. Nothing is signed, notarized or published; only the macOS folder has been started.
- `Swof.Droid` is the Android launcher. It compiles `Swof.Game/*.cs` (all but `Program.cs`) against MonoGame's Android framework and adds only `GameActivity.cs`. It needs the android workload and an Android SDK, so it is not in `Swof.sln` and has not been built on this machine; change it through CI. Game code that must differ on a phone asks `OperatingSystem.IsAndroid()`. Its `ApplicationId` is a placeholder.
- The content tool manifest is `.config/dotnet-tools.json` at the repo root, so both projects' content builds find `mgcb`.
- `matches` fails only when a match does not reach full time, stalls, or loses a player or the ball. The scores it prints are measurements, with one exception: a run of 100 or more matches must also meet the pass marks in `MatchRunner.FailedMarks` (goals, shots, possessions and forward progress for any squads; balance between the teams and shots after a kickoff only with `equal`). The marks are wide on purpose; a change that misses one has changed the game, so look at why before moving a mark. They come from matches with no user team (`userControlledTeam: null`), where neither side is treated as the person's; the first ten seeds are replayed with Home as the user team and its controls worked at random, to check they finish. Every tick of every match is checked against rules that always hold (the clock moves one tick, scores never fall or jump, the period never goes back, at most one player has the ball, nothing becomes NaN or infinite); breaking one fails the match. A seed chooses the eight outfielders' attributes and the small differences in computer decisions (`DecisionNoise`); the same seed always replays the same match, and physics has no randomness.
- A green gate says nothing about game feel. Motion, tempo and touch need hands-on play.
- `tools/check.sh diff` is the check for a change meant not to alter play (a refactor, the renderer, docs): it must show no difference. For a gameplay change it lists the seeds that moved.
- `.claude/settings.json` has a Stop hook, `tools/stop-check.sh`: when code under `Swof.*` or `tools` has changed, it runs the fast gate before an agent stops and sends it back if the gate fails. A tree that passed is remembered in `.git/swof-stop-check-passed`.
- `REVIEW.md` is the review checklist, by what a diff touches.
- A failing scenario is fixed or explained, never skipped, retried, slept around or given a wider tolerance; the engine is deterministic, so a flaky result means something changed. After three rounds of failed fixes on the same failure, stop and report what was tried.

## Architecture Overview

- `Swof.Engine`: dependency-free deterministic 60 Hz match simulation. `MatchEngine.StepTick` is authoritative; hosts render interpolated immutable snapshots. `Swof.Engine/AI` drives computer players through the same one-button `PlayerInput` a human uses.
- `Swof.Engine/MatchFlow.cs` decides which screen is up and what its menu holds; the host draws `Heading` and `Menu`, acts on the `FlowEvent` it returns, and gives the match only `ForMatch(input)`. `Product.Name` is the one place the product's name is written (the tuning folder in `PhysicsTuningStore` is a storage key and stays).
- `Swof.Game`: the primary host (MonoGame DesktopGL). Keyboard, gamepad, and the touch controls, fed by the fingers `TouchPanel` reports and by the left mouse button as one more finger (`Swof.Engine/TouchControls.cs`: drag on the left half is a stick, the right half is the button, except the pause button in the top right corner, `Swof.Engine/PauseMenu.cs`). No touchscreen has been tried.
- `Swof.Windows`: legacy WPF host kept for its live physics-tuning grid. Do not add features to its renderer.
- `Swof.Engine.Tests`: the scenario runner described above.

What does not exist yet, despite appearing in docs as design: a wait before the first kickoff (the game and `matches` open on the centre spot and play is live at once; scenarios and the WPF host still open with the Home fixo on the ball; at any kickoff a person's taker stands behind the ball facing forward and a computer's faces its own half from the far side, the first tap finds the nearest team-mate whatever the stick says, and the other team is held outside the centre circle until the taker plays the ball, carries it out, or four seconds pass, after which the ball is simply open to a challenge; at a kick-in, corner or free kick the other team is held 5 m away the same way, and a taker who has not played the ball after four seconds loses it to the other team; a person's taker at any of these stands until the ball is kicked and the stick only aims, while a computer's taker still runs the ball in, because the computer has no restart play), a way for the person to choose which player they control (when the other team has the ball, or it runs loose after they had it, control moves by itself to the outfielder best placed to defend, but never while the button is held), any foul other than a slide through the carrier's back (that one gives a direct free kick, a penalty when it is in the area, and a 10 m kick from a team's sixth foul of a half; at a penalty or 10 m kick the taker cannot move and everyone else waits behind the ball; no cards or advantage, and the goalkeeper is not held on its line), a four-second count for the goalkeeper or at a kickoff, extra time, AI aftertouch on shots, habits that change (`Swof.Engine/AI/PlayerHabits.cs`: how long a computer player keeps the ball and how much he risks carrying it forward, set per player with `ComputerPlayerController.SetHabits`; only the match runner's `home-habits=` and `away-habits=` set any, nothing learns them, and there are no human traits such as discipline or stress handling), a coach that reads the play rather than the score (three defensive tactics exist as data; launch options pick them, T, gamepad Y or the pause menu sets the person's team's during a match, and an opt-in coach swaps a computer team's by score and time, but nothing turns it on without a launch option, and it reads only the scoreboard and the clock), any screen beyond the match flow in `Swof.Engine/MatchFlow.cs` (a start screen with Play and a difficulty that goes round normal, hard, easy; the pause menu on P or the top right corner with resume, the three tactics and Leave match; at full time the result with New match and Leave match; menus are worked with the stick and button or by touching an item; there are no settings, no team or squad choice, nothing saved between launches), a difficulty that changes how the computer decides (`Swof.Engine/AI/Difficulty.cs`: easy slows the opponent's outfielders, hard gives it the push up tactic and the coach; `--opponent-tactic` and `--opponent-coach` win over the level), touch input tried on a touchscreen (the game reads `TouchPanel` and CI packages it for Android, but nobody has run it on a device; pausing when the app is sent to the background, safe areas and notches are not handled), multiplayer.

What the MonoGame host draws today (layers, art files, colours, camera, effects, on-screen text, where a new renderer plugs in, known oddities) is listed in `docs/renderer-inventory.md`. Read it before changing the look.

## Conventions & Patterns

- C# standards live in `Swof.Engine.Tests/AGENTS.md` and apply to the whole repository.
- Keep game logic in `Swof.Engine`; hosts only read input, step the engine and draw.
- Teams change ends at half-time as a turn of the view only: the simulation keeps Home defending the west goal all match. A host draws `snapshot.AsSeen()` and passes the stick through `snapshot.FromScreen(input)`; anything else that maps screen to court must do the same.
- Gameplay numbers belong in `PhysicsTuning`, not inline constants.
- A player's body (`PlayerRadius`, 0.53 m) is what is drawn and what stops at the boards. Reaching the ball is `MatchEngine.Reach(player)`: the two radii plus `FootReach`. Anything new that asks "can this player get to the ball" uses `Reach`, never the radius alone; `FootReach` is the one number to turn if reach should feel shorter.
- New engine behavior needs a scenario in `Swof.Engine.Tests/Program.cs`.

## SWOF Knowledge Routing

Use the repo-local skills before changing their owning surfaces. They live in `.agents/skills`; `.claude/skills` is a symlink to it so Claude Code loads them by name (`$name` is the Codex spelling):

- `$swof-gameplay-ai` for futsal AI, tactics, coach/difficulty design, possession phases, control handoff, goalkeeper ownership, or multiplayer controller assignment.
- `$swof-game-feel` for responsiveness, controls, match tempo, camera/pitch scale, MonoGame rendering, player feedback, or touchscreen readiness.
- `$swof-validation` to choose, run, and interpret the behavior, build, format, diff, and startup gates.

Project-scoped Codex agents live under `.codex/agents`: `swof_ai_scout` maps AI/control behavior without editing, `swof_feel_auditor` reproduces runtime perception issues, and `swof_validator` runs checks without changing source. Use them only when the user explicitly asks for delegation or parallel agent work.

Keep implemented behavior separate from decided future design. In particular, multiplayer (maximum eight outfield humans with pass-relay ownership for two or three users), coach adaptation, and difficulty layers are architectural decisions, not completed features.
