# SWOS Movement Measurement Setup

This guide sets up a repeatable local pass to estimate SWOS movement timing for:

- acceleration from standstill
- deceleration after release
- 90-degree direction change at speed
- each scenario with and without ball

## Files Added

- Launcher script: `tools/swos/Start-SwosMeasurement.ps1`
- DOSBox profile: `tools/swos/dosbox-swos-measurement.conf`
- Data template: `tools/swos/movement-measurement-template.csv`

## Preconditions

- SWOS install directory exists at `C:\games\swos_96_97\SensiWs9\SOC`
- DOSBox 0.74-3 exists at `C:\Program Files (x86)\DOSBox-0.74-3\DOSBox.exe`

If your paths differ, pass them to the script.

## Start Session

From repo root:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\swos\Start-SwosMeasurement.ps1
```

Path check only (no launch):

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\swos\Start-SwosMeasurement.ps1 -DryRun
```

Alternative paths:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\swos\Start-SwosMeasurement.ps1 `
  -SwosDir "D:\Retro\SWOS\SOC" `
  -DosboxExe "D:\Tools\DOSBox\DOSBox.exe"
```

## Capture Video

Use DOSBox built-in capture:

- `Ctrl+Alt+F5` start/stop AVI capture

For fire-button timing, do not start capture on the action under test: this SWOS installation also treats Ctrl as fire, so the DOSBox shortcut can inject an unwanted action. Start recording before entering the scenario, remap guest fire, or use an external window recorder.

Record one scenario per clip and rename files immediately after each run.

To inspect capture timing metadata:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\swos\Get-AviTiming.ps1 -Path "C:\games\swos_96_97\SensiWs9\SOC\capture\scenario1.avi"
```

## Test Scenarios

Use the same pitch camera/zoom and same player for all runs.

1. `accel` without ball
2. `stop` without ball
3. `turn_90` without ball
4. `accel` with ball
5. `stop` with ball
6. `turn_90` with ball

For each clip:

- start capture 1 second before input
- apply one clean input pattern
- stop capture after the movement settles

## Frame Counting Protocol

Count frames in a frame-step video tool and fill `tools/swos/movement-measurement-template.csv`.

Definitions:

- `event_start_frame`: first frame with intended movement input effect
- `event_90pct_frame`: first frame where speed visually reaches about 90% of steady-state speed
- `event_stop_frame`: first frame where body displacement is negligible

Derived values:

- `accel_frames = event_90pct_frame - event_start_frame`
- `stop_frames = event_stop_frame - event_start_frame`

If capture FPS is 70:

- `seconds = frames / 70`

If capture FPS differs, use actual clip FPS.

## Quality Rules

- Keep emulator profile fixed (`dosbox-swos-measurement.conf`) for all clips in one pass.
- Do not change `cycles` mid-pass.
- Run each scenario at least 3 times and use median frames.
- If a run includes collision/contact/noise, discard and retake.

## What This Gives Us

This does not extract hidden engine constants directly. It gives stable empirical timing targets we can map onto SWOF:

- burst acceleration profile
- stopping profile
- turn penalty profile

Those targets are enough to tune movement curves and verify progress objectively.
