---
name: swof-game-feel
description: Use when diagnosing, designing, implementing, or reviewing SWOF responsiveness, controls, match tempo, camera framing, pitch scale, rendering smoothness, pixel-player animation, action feedback, HUD, audio/haptics, or touchscreen readiness in the MonoGame host. Trigger when the user describes how the running game looks or feels. Do not use for AI-only logic or build-only failures.
---

# SWOF Game Feel

Treat game feel as the complete perception-action loop, not a graphics polish layer. Objective checks establish correctness; the running MonoGame build and the user's perception decide whether a subjective issue is solved.

## Start At The Acceptance Layer

1. Run `bd prime`, preserve unrelated changes, and read [runtime-acceptance.md](references/runtime-acceptance.md).
2. Reproduce the report in `Swof.Game`, the primary playable host. Use `Swof.Windows` only as reference/tuning tooling.
3. Translate the user's words into an observable symptom without overruling them. "Pitch feels too small" may be camera scale; "defenders do nothing" may be control ownership and visibility; "too fast" may combine player speed, pass speed, camera, and decision cadence.
4. Inspect the entire path: input polling and buffering, fixed-tick simulation, immutable snapshots, interpolation, camera, draw batching, feedback events, and persisted tuning.

## Preserve The Presentation Contract

- Keep `MatchEngine.StepTick` authoritative at 60 Hz. Let `MatchEngine.Step(deltaSeconds, input)` accumulate host time and cap extreme frame deltas.
- Render `GetRenderSnapshot()` interpolation without mutating simulation state.
- Keep world positions and camera transforms floating-point. Pixel art can be point-sampled while moving subpixel-smoothly.
- Build or prepare shared render targets before the gameplay `SpriteBatch.Begin`; never nest `Begin` calls.
- Cache static pitch/stadium geometry and batch dynamic players, ball, HUD, and overlays.
- Keep both the ball and human-owned player visible when control transitions make that necessary.

## Build A Coherent Feedback Loop

For each important action, connect:

```text
intent -> acknowledgement -> contact -> physical consequence -> social reaction -> memory
```

Make contact-tick output authoritative. Animation communicates accepted input and consequences; it must not delay the requested pass, shot, tackle, or save. Effects should be brief, local, directional, proportional, and readable.

Preserve `Dry`, `Connected`, and `Arcade` modes for A/B comparison. Keep camera shake and secondary motion optional. Favor stable player identity, active-control feedback, nearby reactions, and action audio before indiscriminate particles.

## Protect The Control Grammar

- Preserve one stick plus one context-sensitive action button for eventual phone/tablet play unless real-device evidence disproves it.
- Buffer press and release edges until a simulation tick consumes them.
- Keep tap-pass, hold-shot/tackle, and post-contact directional aftertouch understandable from the same grammar.
- The goalkeeper is user-controlled only for held-ball distribution; aiming distribution must not accidentally move the keeper.
- Do not claim mobile readiness from desktop reasoning alone. Validate stable touch IDs, safe areas, target sizes, missed taps, unintended holds, and haptic/accessibility behavior on a real device.

## Iterate In Measurable Slices

Change the smallest set that can affect the reported sensation. Add deterministic assertions for mechanics, launch a captured-stderr smoke test for startup/rendering faults, then ask the user to play the exact scenario when the acceptance criterion is perceptual.

Keep a subjective Beads issue open until hands-on confirmation. Report what automation proves separately from what only play can prove.

Use `$swof-validation` to select and run the objective gates.
