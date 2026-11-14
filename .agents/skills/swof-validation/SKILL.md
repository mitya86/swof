---
name: swof-validation
description: Use when choosing, running, or interpreting validation for changes in the SWOF .NET/MonoGame repository, including the executable behavior scenarios, solution tests/build, formatting, diff checks, MonoGame startup smoke, persisted-tuning checks, and environment-failure triage. Trigger before completing implementation or when checks fail. Do not treat automated validation as proof of subjective game feel.
---

# SWOF Validation

Run the smallest credible gate set for the change, then separate source failures, environment failures, startup integration, and hands-on acceptance.

## Choose A Gate Set

Read [quality-gates.md](references/quality-gates.md), then run a gate from the repository root.

On any OS (this is what CI runs):

```bash
tools/check.sh                # fast: behavior scenarios + git diff --check
tools/check.sh fast "tackle"  # only scenarios whose name contains the filter
tools/check.sh full           # fast + MonoGame host build + format verification
```

On Windows, the PowerShell script additionally builds the whole solution (including the WPF host) and has the startup smoke mode:

```powershell
.\.agents\skills\swof-validation\scripts\Invoke-SwofChecks.ps1 -Mode Fast
.\.agents\skills\swof-validation\scripts\Invoke-SwofChecks.ps1 -Mode Full
.\.agents\skills\swof-validation\scripts\Invoke-SwofChecks.ps1 -Mode Smoke
```

- Use `Fast` while iterating on engine behavior: executable behavior scenarios plus `git diff --check`.
- Use `Full` before handing off code: behavior scenarios, pinned MonoGame tool availability/restore, `dotnet test`, solution build, formatting verification, and diff check.
- Use `Smoke` after MonoGame rendering/content/startup changes. It treats a game that remains alive for the requested interval as a startup pass and captures stdout/stderr.
- For skills, agent configuration, or documentation-only changes, validate those artifacts directly and run `git diff --check`; do not spend time rebuilding unrelated game code.

## Interpret Results

- `Swof.Engine.Tests` is a dependency-free executable scenario runner. `dotnet test` alone can build it without executing its scenarios, so always run the project explicitly for behavior coverage.
- A deterministic scenario failure is a product regression until evidence shows the scenario is invalid.
- `NU1301` usually means NuGet access failed. Retry with approved network access before blaming source.
- `NU1900` can be unreachable vulnerability metadata; report it, but distinguish a warning from a compiler failure.
- Missing `mgcb` after `--no-restore` means the local MonoGame tool may need `dotnet tool restore --tool-manifest Swof.Game\.config\dotnet-tools.json`.
- A build or eight-second smoke cannot prove that motion is smooth, tempo is readable, or touch feels good. Mark those as awaiting play confirmation.
- If changed defaults appear ignored, inspect the loaded profile, `SWOF/physics-tuning.json` under the OS local application-data folder (`%LOCALAPPDATA%` on Windows), and `PhysicsTuning.CurrentProfileVersion`.

## Handoff Evidence

Report commands, scenario count, errors/warnings, smoke duration, and any unverified perceptual criterion. Preserve unrelated worktree changes and do not commit or push without authority.
