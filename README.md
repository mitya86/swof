# SWOF Engine Prototype

This repository starts a clean .NET football engine inspired by one-button arcade football controls.

## Projects

- `Swof.Engine`: portable match simulation, rules, physics, and state.
- `Swof.Game`: primary MonoGame 3.8.5 desktop host with fixed-step simulation, interpolated presentation, and batched rendering.
- `Swof.Windows`: retained WPF tuning and reference host during the MonoGame transition.
- `Swof.Engine.Tests`: small dependency-free behavior test runner.

## Current Controls

- Arrow keys, WASD, D-pad, or left stick: move the active player.
- Space or gamepad A: hold and release to kick when in possession, or tackle when out of possession.
- R or gamepad Start: resume after an out-of-play event.
- F1: reload the persisted physics-tuning profile.
- F11: toggle fullscreen.
- Escape or gamepad Back: quit.

## MVP Behaviors

- Field dimensions and goals.
- Several stationary players.
- Original pixel-art footballer sprites with smooth directional motion and dynamic ground shadows.
- One active user-controlled player.
- Possession, dribbling, loose ball movement, kicking, and simple tackling.
- Goal detection.
- Touchline and goal-line out detection.

## Simulation And Rendering

The engine advances through deterministic fixed ticks with `MatchEngine.StepTick(input)`.

`Swof.Game` presents at the VSync-limited display rate while `MatchEngine.Step(deltaSeconds, input)` accumulates wall-clock time into fixed simulation ticks. Its renderer interpolates immutable previous/current snapshots, damps camera lookahead over real frame time, samples the pitch with a subpixel camera transform, caches the static pitch and stadium in a render target, and batches only dynamic match objects each frame.

Other hosts can call `MatchEngine.Step(deltaSeconds, input)` to accumulate wall-clock time into fixed ticks. Rendering can use `MatchEngine.GetRenderSnapshot()`, which interpolates between immutable previous/current simulation snapshots without mutating authoritative state.

This keeps match logic replayable while allowing smooth modern rendering over pixel-art presentation.

## Research Notes

- [SWOS-inspired physics contract](docs/research/swos-physics-contract.md)
- [SWOS local file inventory](docs/research/swos-file-inventory.md)
- [SWOS player facing notes](docs/research/swos-player-facing.md)
- [SWOS dribbling and ball control notes](docs/research/swos-dribbling-and-ball-control.md)
- [SWOS movement measurement setup](docs/research/swos-movement-measurement-setup.md)

## Useful Commands

```powershell
dotnet run --project Swof.Game\Swof.Game.csproj
dotnet run --project Swof.Windows\Swof.Windows.csproj
dotnet run --project Swof.Engine.Tests\Swof.Engine.Tests.csproj
dotnet build Swof.sln
```

The repository uses the .NET 10 SDK. MonoGame content tools are restored automatically by the game project.

## Debugging

Visual Studio Code:

- Open this folder.
- Press F5.
- Choose `SWOF Game (MonoGame)` if prompted.

Visual Studio:

- Open `Swof.sln`.
- Set `Swof.Game` as the startup project.
- Press F5.
