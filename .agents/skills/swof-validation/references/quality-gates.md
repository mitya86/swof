# SWOF Quality Gates

## Commands

Restore the pinned MonoGame content tool after cloning or when `mgcb` is missing:

```powershell
dotnet tool restore --tool-manifest Swof.Game\.config\dotnet-tools.json
```

Run the executable deterministic scenarios:

```powershell
dotnet run --project Swof.Engine.Tests\Swof.Engine.Tests.csproj --no-restore
```

Run the repository-required .NET test/build checks:

```powershell
dotnet test Swof.sln --no-restore
dotnet build Swof.sln --no-restore
```

Verify formatting and patch whitespace:

```powershell
dotnet format Swof.sln --no-restore --verify-no-changes
git diff --check
```

Launch the primary playable host:

```powershell
dotnet run --project Swof.Game\Swof.Game.csproj --no-build
```

Repeatable presentation modes:

```powershell
dotnet run --project Swof.Game\Swof.Game.csproj --no-build -- --feedback=dry --demo-action=pass
dotnet run --project Swof.Game\Swof.Game.csproj --no-build -- --feedback=connected --demo-action=shot
dotnet run --project Swof.Game\Swof.Game.csproj --no-build -- --feedback=arcade --demo-action=shot
```

## Change-To-Gate Matrix

| Change | Required evidence |
| --- | --- |
| Engine physics, controls, rules | Focused deterministic scenarios, full checks |
| AI pattern/instruction/tactics | Positive, negative, competition, deterministic tie, human-slot scenarios; full checks; live two-team play |
| Possession or active-player handoff | Both-team transitions, passes, interception/tackle/GK collection, camera/marker play check |
| Renderer, camera, content | Full checks, captured-stderr smoke, hands-on visual confirmation |
| Tempo or tuning | Speed-envelope scenarios, persisted-profile check, smoke, hands-on pacing confirmation |
| Touchscreen design | Desktop invariants plus real-device touch/haptic/accessibility tests |
| Docs, skills, agent TOML | Relevant structural validator/TOML parse and `git diff --check` |

## Failure Classification

- **Source:** compile error, analyzer error, deterministic scenario failure, formatter diff, startup exception.
- **Environment:** blocked NuGet/TLS, missing restored tool, sandboxed GUI/process access.
- **Persisted state:** local tuning overrides new defaults or has an old schema/version.
- **Subjective acceptance:** running result remains jumpy, unclear, too fast, too small, disconnected, or uncomfortable despite objective passes.

Never collapse these categories into “tests passed” or “tests failed.” State what the evidence actually establishes.
