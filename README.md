# SWOF Engine Prototype

This repository starts a clean .NET football engine inspired by one-button arcade football controls.

## Projects

- `Swof.Engine`: portable match simulation, rules, physics, and state.
- `Swof.Windows`: WPF viewer/controller for the current MVP.
- `Swof.Engine.Tests`: small dependency-free behavior test runner.

## Current Controls

- Arrow keys: move the active player.
- Space: hold and release to kick when in possession, or tackle when out of possession.
- R: resume after an out-of-play event.

## MVP Behaviors

- Field dimensions and goals.
- Several stationary players.
- One active user-controlled player.
- Possession, dribbling, loose ball movement, kicking, and simple tackling.
- Goal detection.
- Touchline and goal-line out detection.

## Simulation And Rendering

The engine advances through deterministic fixed ticks with `MatchEngine.StepTick(input)`.

Hosts can call `MatchEngine.Step(deltaSeconds, input)` to accumulate wall-clock time into fixed ticks. Rendering should use `MatchEngine.GetRenderSnapshot()`, which interpolates between immutable previous/current simulation snapshots without mutating authoritative state.

This keeps match logic replayable while allowing smooth modern rendering over pixel-art presentation.

## Research Notes

- [SWOS-inspired physics contract](docs/research/swos-physics-contract.md)
- [SWOS local file inventory](docs/research/swos-file-inventory.md)
- [SWOS player facing notes](docs/research/swos-player-facing.md)

## Useful Commands

```powershell
dotnet run --project Swof.Windows\Swof.Windows.csproj
dotnet run --project Swof.Engine.Tests\Swof.Engine.Tests.csproj
dotnet build Swof.Windows\Swof.Windows.csproj
dotnet build Swof.Engine\Swof.Engine.csproj
```

## Debugging

Visual Studio Code:

- Open this folder.
- Press F5.
- Choose `SWOF Windows` if prompted.

Visual Studio:

- Open `Swof.sln`.
- Set `Swof.Windows` as the startup project if Visual Studio does not pick it automatically.
- Press F5 and use the `SWOF Windows` profile.
