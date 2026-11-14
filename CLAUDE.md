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
tools/check.sh matches 100 1 equal   # every outfielder identical, to judge balance without mismatches
dotnet run --project Swof.Game/Swof.Game.csproj   # play it
```

If bare `dotnet` fails (a mise shim with no version set), prefix it: `mise exec dotnet@10 -- dotnet ...`.

- `Swof.Engine.Tests` is an executable scenario runner, not a test-framework project. `dotnet test` builds it without running anything, so never use `dotnet test` as evidence.
- `Swof.sln` includes the WPF host and only builds on Windows. On macOS/Linux build the projects individually.
- CI (`.github/workflows/ci.yml`) runs the fast gate on Linux and builds the full solution on Windows.
- `matches` fails only when a match does not reach full time, stalls, or loses a player or the ball. The scores it prints are measurements, not a pass mark. A seed chooses the eight outfielders' attributes and the small differences in computer decisions (`DecisionNoise`); the same seed always replays the same match, and physics has no randomness.
- A green gate says nothing about game feel. Motion, tempo and touch need hands-on play.

## Architecture Overview

- `Swof.Engine`: dependency-free deterministic 60 Hz match simulation. `MatchEngine.StepTick` is authoritative; hosts render interpolated immutable snapshots. `Swof.Engine/AI` drives computer players through the same one-button `PlayerInput` a human uses.
- `Swof.Game`: the primary host (MonoGame DesktopGL). Keyboard and gamepad only; there is no touch input yet.
- `Swof.Windows`: legacy WPF host kept for its live physics-tuning grid. Do not add features to its renderer.
- `Swof.Engine.Tests`: the scenario runner described above.

What does not exist yet, despite appearing in docs as design: fouls and free kicks, the four-second count, changing ends at half-time, extra time, starting a new match without relaunching, AI aftertouch on shots, tactics selection, menus, touch, multiplayer.

## Conventions & Patterns

- C# standards live in `Swof.Engine.Tests/AGENTS.md` and apply to the whole repository.
- Keep game logic in `Swof.Engine`; hosts only read input, step the engine and draw.
- Gameplay numbers belong in `PhysicsTuning`, not inline constants.
- New engine behavior needs a scenario in `Swof.Engine.Tests/Program.cs`.

## SWOF Knowledge Routing

Use the repo-local skills before changing their owning surfaces. They live in `.agents/skills`; `.claude/skills` is a symlink to it so Claude Code loads them by name (`$name` is the Codex spelling):

- `$swof-gameplay-ai` for futsal AI, tactics, coach/difficulty design, possession phases, control handoff, goalkeeper ownership, or multiplayer controller assignment.
- `$swof-game-feel` for responsiveness, controls, match tempo, camera/pitch scale, MonoGame rendering, player feedback, or touchscreen readiness.
- `$swof-validation` to choose, run, and interpret the behavior, build, format, diff, and startup gates.

Project-scoped Codex agents live under `.codex/agents`: `swof_ai_scout` maps AI/control behavior without editing, `swof_feel_auditor` reproduces runtime perception issues, and `swof_validator` runs checks without changing source. Use them only when the user explicitly asks for delegation or parallel agent work.

Keep implemented behavior separate from decided future design. In particular, multiplayer (maximum eight outfield humans with pass-relay ownership for two or three users), coach adaptation, and difficulty layers are architectural decisions, not completed features.
