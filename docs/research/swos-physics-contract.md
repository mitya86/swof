# SWOS-Inspired Physics Contract

This document describes the deterministic behavior we want to recover from observation and implement in `Swof.Engine`.

We are not extracting original SWOS code. The goal is a clean-room set of rules and constants that creates a similar match feel.

## Source Copy

Local reference install:

```text
C:\games\swos_96_97\SensiWs9\SOC
```

Observed main program/resource candidates:

- `SWS.EXE`: main DOS game executable.
- `PITCH1.DAT` through `PITCH6.DAT`: pitch data candidates.
- `PITCH1.BLK` through `PITCH6.BLK`: pitch graphics/block candidates.
- `SPRITE.DAT`: sprite data candidate.
- `DEMO.DAT`: demo/playback candidate worth investigating for observable input/state patterns.
- `TEAM1.DAT`, `TEAM2.DAT`, `TEAM3.DAT`, `DATA\TEAM.*`: team/player data candidates.

## Determinism Requirements

The match simulation must be deterministic for a fixed initial state and fixed input sequence.

- Use a fixed tick rate.
- Keep simulation state independent of rendering.
- Avoid wall-clock time inside the engine.
- Avoid platform-specific floating point surprises where possible.
- Record inputs per tick, not per frame.
- All randomness must come from an explicit seeded generator.

Initial engine target:

```text
Tick rate: 60 Hz
Coordinate system: field center = (0, 0)
Positive X: Home attacks to the right
Positive Y: down the screen
```

The engine exposes `StepTick(input)` as the authoritative simulation operation. Hosts can call `Step(deltaSeconds, input)` as a convenience accumulator, but tests and future networking/replay code should use ticks directly.

## Rule Areas To Recover

### Player Movement

Rules we need:

- max run speed
- acceleration from rest
- deceleration after releasing movement
- turn rate or instant direction change
- diagonal movement normalization
- possession speed modifier
- tackle/slide speed and duration
- player collision radius
- boundary clamping or sliding along pitch edges

Questions:

- Does SWOS preserve momentum when changing direction sharply?
- Does dribbling slow the player?
- Are diagonals exactly normalized or faster due to digital input?
- Is running speed attribute-driven?

### Ball Movement

Rules we need:

- ground-ball initial speed by action hold time
- vertical lift/height for long passes and hard kicks
- ball deceleration/friction curve
- gravity and bounce decay
- minimum speed snap-to-rest threshold
- shot/pass direction quantization
- collision radius
- wall/goal frame behavior, if any
- ball carry offset while possessed
- loose ball collection distance

Questions:

- Is ball friction linear, multiplicative, or table-driven?
- Does ball direction update while the button is held or only on release?
- Does a standing kick differ from a running kick?
- Is there a separate pass mode for taps?

### Possession

Rules we need:

- who owns the ball when player and ball overlap
- collection cooldown after a kick
- contested possession resolution
- ball position while dribbling
- first-touch behavior
- whether ball can be poked away during possession

Questions:

- Does possession require approaching from a certain angle?
- Does the active player auto-control loose balls?
- How often does the ball detach during fast turns?

### One-Button Action

Rules we need:

- tap threshold
- hold-to-power curve
- pass/shot/clear context rules
- tackle behavior without possession
- heading/volley behavior later

Questions:

- What is the minimum kick power?
- How long to reach maximum power?
- Does power continue charging after max?
- Does context choose teammate pass, goal shot, or pure directional kick?

Manual-supported behavior:

- quick fire-button tap while in possession attempts a pass
- longer hold kicks in the player's facing direction
- longer hold means harder kick
- joystick input after the kick controls bend/lift/drive
- keeping fire held after the kick changes shot behavior through teammates

Current clean-room placeholder:

```text
0-7 held ticks: pass
8+ held ticks: kick
45 held ticks: maximum kick power
```

Binary probe note:

`SWS.EXE` contains many little-endian occurrences of common small values, so raw constant scanning does not identify exact gameplay thresholds on its own.

```text
3: 110167 occurrences
4: 3134
5: 6920
6: 864
7: 5221
8: 2253
9: 1380
10: 3726
11: 9593
12: 5282
15: 532
18: 342
20: 289
30: 170
45: 112
60: 161
```

Candidate constants must be found by correlating runtime behavior or disassembly context, not by byte frequency.

### Pitch Rules

Rules we need:

- pitch bounds
- goal opening bounds
- goal scored detection
- touchline out
- goal-line out
- restart classification: throw-in, goal kick, corner, kickoff

Questions:

- Is the whole ball required to cross the line, or just ball center?
- Does possession at the boundary trigger out immediately?
- How does the game decide corner versus goal kick?

## Measurement Plan

Each measurement should produce a note, a rough constant, and a test or tuning target.

### Captured Video Measurements

Use a stable DOSBox resolution and record short clips:

- player starts from rest and runs horizontally for 2 seconds
- player releases movement from max speed
- player reverses direction at max speed
- player dribbles horizontally for 2 seconds
- tap kick from standing start
- short, medium, and max held shots
- ball rolling until it stops
- goal and out-of-play crossings

For each clip:

- count frames
- record screen-space position over time
- fit approximate velocity and friction curves
- translate screen pixels to field units using pitch line landmarks

### Manual Stopwatch Measurements

Useful quick checks:

- time from midfield to penalty box
- time from one touchline to the other
- time for a max-power shot to cross half the pitch
- time for ball to stop after a tap

### Engine Comparison

For every recovered behavior:

- add a deterministic test when the result is exact
- add a tuning note when the result is feel-based
- keep constants centralized so they can be adjusted without changing rules

## Initial Clean-Room Model

These are current placeholders in `Swof.Engine`, not claimed SWOS values.

```text
Tick rate: 60 Hz
Player max speed: 0.300 field units / tick
Player acceleration: 0.0194 field units / tick^2
Player friction: 0.0025 field units / tick^2
Ball friction: 0.00144 field units / tick^2
Ball gravity: 0.0128 field units / tick^2
Ball bounce energy retention: 0.42
Long-pass lift: 0.120 field units / tick
Kick hold max time: 45 ticks
Tap pass max time: 7 ticks
Pass speed: 0.400 field units / tick
Kick speed range: 0.300 to 0.700 field units / tick
Kick lift range: 0.030 to 0.320 field units / tick
Collection cooldown after kick: 11 ticks
```

## Next Rules To Add

- previous-touch tracking for corner vs goal kick
- deterministic replay input log
- player switching
- collision/contested possession rules
- measured ball friction curves
- measured tap/pass and kick-power thresholds
