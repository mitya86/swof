# SWOF Player Movement Model

SWOF uses deterministic eight-way movement at 60 simulation ticks per second. The model aims for the quick intent and readable momentum of an arcade football game without copying hidden SWOS rules or constants.

## Control Response

- Facing follows the requested eight-way direction immediately.
- Velocity accelerates progressively, reaching normal running pace in a small number of ticks.
- Continuing forward uses a burst-to-sustain acceleration curve.
- A 90-degree turn gets a dedicated steering response so top-speed sustain acceleration cannot make controls sluggish.
- A reversal gets stronger braking response, but still crosses through a short momentum phase instead of teleporting velocity.
- Releasing input brakes promptly and retains the last facing direction for passes, shots, and dribble presentation.
- Diagonal input is normalized to the same speed cap as cardinal input.

## Boundaries

Player position is resolved before ball control for the tick. Contact with a pitch edge cancels only the velocity component pointing out of bounds, leaving tangential velocity available so the player can run along the line without sticking.

## Ball-Control Relationship

Facing and movement converge quickly enough that the visible player intent, physical travel, and dribble correction agree. A single deliberate turn is recoverable. Repeated high-speed direction changes can still produce a deterministic mistouch, especially for a low-control player.

The important contract is response timing and behavior shape. Tuning values remain centralized in `PhysicsTuning` so empirical capture measurements can refine the numbers later without replacing the movement rules.
