# SWOF Engine Prototype

This repository starts a clean .NET 5v5 futsal engine inspired by one-button arcade football controls.

## Projects

- `Swof.Engine`: portable match simulation, rules, physics, and state.
- `Swof.Game`: primary MonoGame 3.8.5 desktop host with fixed-step simulation, interpolated presentation, and batched rendering.
- `Swof.Windows`: retained WPF tuning and reference host during the MonoGame transition.
- `Swof.Engine.Tests`: small dependency-free behavior test runner.
- `Swof.Droid`: optional Android host that compiles the same game sources against MonoGame 3.8.5 for Android. It is not in `Swof.sln`; it needs the `android` workload and an Android SDK.

## Current Controls

- Arrow keys, WASD, D-pad, left stick, or drag on the left half of the screen: move the active outfield player. When the goalkeeper holds the ball, aim distribution without moving the keeper.
- Space, gamepad A, or tap the right half of the screen: tap for a targeted pass, hold beyond the tap window to shoot, or tackle when out of possession.
- Direction immediately after shot contact: bend sideways, drive forward, or loft backward relative to the shot.
- P, or the pause button in the top-right corner: open the pause menu to resume, change tactic, or leave the match. T or gamepad Y also cycles the team's tactic (High Press, Half Court, Push Up).
- R or gamepad Start: skip the short pause before a restart. Play resumes on its own after about a second.
- A match is two three-minute halves on a stopped clock (`HalfLengthSeconds` in the tuning). The host opens on a start screen (Play / Difficulty). After full time, a result screen offers a new match or a return to the start screen.
- F1: reload the persisted physics-tuning profile.
- F2: cycle between dry, connected, and arcade action feedback for direct comparison.
- F3: toggle the compact match HUD and full diagnostic HUD.
- F4: toggle the optional camera contact response (disabled by default).
- F11: toggle fullscreen.
- Escape or gamepad Back: quit.

For repeatable presentation captures, the MonoGame host also accepts
`--feedback=dry|connected|arcade` and `--demo-action=pass|shot`. These launch-only
options play one deterministic input sequence and do not alter normal controls.

A career run is started with `--career=<path>`. The host loads or creates a squad and coach at that file, and it develops and saves them at each full time.

## MVP Behaviors

- A 40m by 20m indoor futsal court with four outfielders and one goalkeeper per team.
- Independently evaluated AI players that preserve their futsal roles, seek open passing positions, carry into safer space, and use normal one-button inputs to circulate possession at a readable tempo.
- Defensive transitions automatically select a nearby goal-side user defender; one teammate covers the press while the other two retain distinct marks in a compact structure.
- Camera framing keeps both the ball and the selected defender in view, with a persistent yellow control ring and marker.
- Compact futsal tackles have a brief travel window and quick recovery instead of a long arcade slide.
- A closer follow camera for playable scale.
- Original 24x32-cell, reduced-palette footballer sprites with smooth directional motion and dynamic ground shadows.
- One active user-controlled player, with goalkeeper control limited to held-ball distribution.
- Possession, dribbling, loose ball movement, kicking, and simple tackling.
- Goal detection.
- Kick-ins, corners and goal clearances awarded against the team that last touched the ball, resuming automatically. The taker is placed on the spot and opponents are moved back 5 m; the match clock stops while the ball is out.
- Start, pause, and full-time menus, plus an optional career mode that keeps a squad and coach between launches.

## Simulation And Rendering

The engine advances through deterministic fixed ticks with `MatchEngine.StepTick(input)`.

`Swof.Game` presents at the VSync-limited display rate while `MatchEngine.Step(deltaSeconds, input)` accumulates wall-clock time into fixed simulation ticks. Its renderer interpolates immutable previous/current snapshots, damps camera lookahead over real frame time, samples the pitch with a subpixel camera transform, caches the static pitch and stadium in a render target, and batches only dynamic match objects each frame. Players use point-sampled low-resolution art at floating-point screen positions, keeping their pixels crisp without tying movement to the sprite grid.

Other hosts can call `MatchEngine.Step(deltaSeconds, input)` to accumulate wall-clock time into fixed ticks. Rendering can use `MatchEngine.GetRenderSnapshot()`, which interpolates between immutable previous/current simulation snapshots without mutating authoritative state.

This keeps match logic replayable while allowing smooth modern rendering over pixel-art presentation.

## Research Notes

- [SWOS-inspired physics contract](docs/research/swos-physics-contract.md)
- [SWOS local file inventory](docs/research/swos-file-inventory.md)
- [SWOS player facing notes](docs/research/swos-player-facing.md)
- [SWOS dribbling and ball control notes](docs/research/swos-dribbling-and-ball-control.md)
- [SWOS one-button action and aftertouch notes](docs/research/swos-one-button-action.md)
- [SWOS goalkeeper control and match-person model](docs/research/swos-goalkeeper-control.md)
- [Player connection, responsiveness, and game-feel research](docs/research/player-connection-and-game-feel.md)
- [SWOS movement measurement setup](docs/research/swos-movement-measurement-setup.md)

## Useful Commands

```powershell
dotnet tool restore --tool-manifest .config\dotnet-tools.json
dotnet run --project Swof.Game\Swof.Game.csproj
dotnet run --project Swof.Windows\Swof.Windows.csproj
dotnet run --project Swof.Engine.Tests\Swof.Engine.Tests.csproj
dotnet build Swof.sln
.\.agents\skills\swof-validation\scripts\Invoke-SwofChecks.ps1 -Mode Fast
.\.agents\skills\swof-validation\scripts\Invoke-SwofChecks.ps1 -Mode Full
.\.agents\skills\swof-validation\scripts\Invoke-SwofChecks.ps1 -Mode Smoke
```

On macOS or Linux, `tools/check.sh fast` (or `full`) is the same quality gate used in CI.

The repository uses the .NET 10 SDK. MonoGame content tools are pinned in the repository-root tool manifest (`.config/dotnet-tools.json`) and restored automatically during builds; the explicit restore command above is safe to run after cloning.

The three `Invoke-SwofChecks` modes run the executable behavior scenarios plus progressively broader quality gates. `Smoke` launches the MonoGame host for a bounded interval and captures startup errors; it does not replace hands-on evaluation of motion, pacing, controls, or touch.

## Debugging

Visual Studio Code:

- Open this folder.
- Press F5.
- Choose `SWOF Game (MonoGame)` if prompted.

Visual Studio:

- Open `Swof.sln`.
- Set `Swof.Game` as the startup project.
- Press F5.
