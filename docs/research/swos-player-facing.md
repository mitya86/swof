# SWOS Player Facing Notes

## Summary

SWOS treats player facing as a real gameplay concept, not merely a cosmetic sprite rotation.

The manual repeatedly defines pass and shot direction as the direction the player is facing. It also says dribbling is not glued to the player's feet, which implies facing, running direction, ball carry, and control quality are all related but not identical.

## Source Observations

Manual behavior:

- Moving the joystick in eight directions runs the player in that direction.
- With the ball, a quick fire-button tap attempts a pass to the nearest teammate in the general direction the player is facing.
- Holding fire kicks the ball straight in the direction the player is facing.
- Aftertouch is applied after the kick, so initial facing still matters.
- The ball is not glued to the player's feet; faster running makes control harder.

Local file observations:

- `C:\games\swos_96_97\SensiWs9\SOC\TEAM1.DAT`
- `C:\games\swos_96_97\SensiWs9\SOC\TEAM2.DAT`
- `C:\games\swos_96_97\SensiWs9\SOC\TEAM3.DAT`

Each file is 45,648 bytes, exactly 317 records of 144 bytes.

The first records decode cleanly as small planar sprite frames with dimensions such as 12x15, 13x15, 11x15, and 9x15 pixels. These frames show different silhouettes and color/detail placement consistent with front, back, and side/diagonal player views.

The three `TEAM*.DAT` files share the same frame structure and differ in uniform/detail bytes, which matches the idea that SWOS recolors or swaps team/player strips rather than rotating one generic dot.

## Design Implications For SWOF

We should model facing explicitly:

```text
Player position
Player velocity
Player facing azimuth
Player action state
Player animation frame
Ball carry/contact point
```

Facing should be stored as an azimuth in degrees, then quantized to eight sectors for now:

```text
0 = north/up screen
90 = east/right screen
180 = south/down screen
270 = west/left screen
```

The initial engine can snap input to 45 degree increments. Later, we can smooth rotation or add finer directional sectors without changing the shape of player state.

Movement input should normally update facing, but facing should not be treated as a free-floating visual. It should drive:

- pass target cone
- shot initial direction
- tackle direction
- dribble carry offset
- sprite selection
- optional turn delay or ball-control loss later

## Open Questions

- Does SWOS instantly turn to the new joystick direction, or is there a one-frame/action-state delay?
- Does the player retain facing when movement input is released?
- Does possession use facing direction, velocity direction, or a separate ball contact direction?
- How many animation frames exist per facing direction and per action state?
- Are diagonal frames unique or mirrored from cardinal/side frames?

## Recommended Next Engine Step

Add explicit `Facing` azimuth to `PlayerState`, with:

- eight-sector quantization from input
- facing retained when input is neutral
- ball dribble offset based on facing
- kick/pass/tackle based on facing, not raw current input
- WPF renderer changed from circles to simple directional bodies

This gets the engine closer to SWOS behavior without committing us to exact original sprite decoding yet.
