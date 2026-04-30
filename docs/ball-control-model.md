# SWOF Ball Control Model

This note describes how controlled dribbling currently works in SWOF.

## Overview

Ball control is implemented as layered behavior per simulation tick, not as a hard lock.

1. Soft velocity coupling:
The controlled ball velocity is moved toward player velocity (`DribbleControlFrictionPerTick`), so the ball follows but does not snap.

2. Moving front alignment:
When the possessor is moving, the ball is corrected toward a forward offset based on player facing.
Side offset and side velocity are reduced each tick.

3. Stationary settle/trap:
At low possessor speed, the ball is settled to a resting lead in front of the player and clamped inside a possession radius.

4. Stride touches:
At step-distance intervals, a forward touch impulse is applied.
This refreshes a short secure window and helps maintain running control rhythm.

5. Risk-based control break:
A dribble-risk accumulator is updated every tick from:
- turn severity
- side drift and side-drift change
- excess ball distance from ideal lead
- sustained-running pressure over time
If risk crosses `DribbleRiskBreakThreshold`, possession is lost.

6. Mistouch release:
On control break, the ball is released forward-biased with a small turn-dependent lateral wobble, plus collection cooldown to avoid instant self-recapture.

## Key State Variables

- `_dribbleRisk`: current break risk.
- `_dribbleSustain01`: sustained-running pressure ramp.
- `_dribbleSecureTicksRemaining`: short post-touch protection window.
- `_previousDribbleMoveDirection`, `_previousDribbleSideDistance`, `_lastDribbleTurn01`: context used to estimate careless movement and release shape.

## Why This Is Not "Glued"

The model intentionally allows failure under prolonged or careless dribbling:
- sustained running increases risk even with mostly straight movement
- low-control players accumulate risk faster
- possession breaks can happen without tackles when risk is high enough

This is intended to approximate SWOS-like contestable dribbling while remaining deterministic and tuneable.
