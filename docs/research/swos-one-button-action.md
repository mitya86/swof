# SWOS One-Button Action And Aftertouch

This note separates behavior supported by the original documentation from SWOF's clean-room, modernized implementation.

## Original behavior supported by the manual

The [SWOS 96/97 manual](https://www.abandonwaredos.com/docawd.php?idg=3299&sf=sensibleworldofsoccermanual.txt&sg=Sensible+World+of+Soccer+96%2F97&st=manual) describes one context-sensitive fire button:

- A quick tap in possession attempts a targeted pass to the nearest teammate in the general facing direction.
- A longer hold kicks straight along the player's facing direction, with more hold producing more power.
- Keeping fire held after contact lets the shot travel through friendly players in its path.
- Direction immediately after contact is aftertouch. Left/right bends the ball. Direction toward the opposing goal drives it lower; direction away lifts it higher; neutral produces the default lob.
- Earlier post-contact direction has more influence.
- The normal kick model also applies to throw-ins, corners, goal kicks, and penalties.
- Without possession, fire normally starts a sliding tackle. Near an airborne ball it instead attempts a header. Direction and hold duration control a successful tackle/header deflection.
- When the ball is out, tapping one direction three times opens the manager bench.

The manual establishes the behavior shape but does not publish frame thresholds or a precise contact-frame state machine.

## Local harness evidence

The local DOS build was run through `tools/swos/Start-SwosMeasurement.ps1` with:

```text
DOSBox cycles: fixed 25000
Capture size: 320x200
Measured capture rate: 70.086908 fps
```

A friendly match and a stationary penalty/set-piece state were reached with injected digital direction and fire input. This confirmed that the same one-button path is active in live play and set pieces.

DOSBox's built-in AVI shortcut is `Ctrl+Alt+F5`, while this SWOS installation also treats Ctrl as fire. Starting a capture can therefore contaminate the exact action being measured. Captures made this way remain useful for visual trajectory inspection, but they are not clean evidence for the exact press/contact/release frame. Do not promote the current 7-tick tap boundary to an original SWOS constant without an external recorder or a remapped guest fire key.

## SWOF clean-room behavior

SWOF keeps the one-button decision while removing the avoidable key-up delay:

1. Pressing action starts a visible anticipation pose immediately.
2. Releasing within `TapPassMaxTicks` performs the targeted pass.
3. Still holding on the next tick commits a directional shot immediately. It no longer waits for key release.
4. Contact applies one complete launch impulse using `ImmediateKickPower01`. No later hold tick can add forward speed or lift. Continued hold only prevents friendly players from collecting the shot.
5. The first local left/right input adds a bounded sidespin impulse. Local forward or backward can drive or loft the ball once, only during the short `AftertouchHeightInputTicks` contact window.
6. Airborne sidespin bends the trajectory. Grass sharply reduces both the Magnus force and remaining spin, so a rolling ball settles into a straight path instead of being continuously steered.
7. Aftertouch strength decays across `AftertouchWindowTicks`, so early input matters most.

### Eight-way aim interpretation

The manual's distinction between a pass in the "general direction" and a kick "straight in the direction" is important: pass help should find an intended receiver, but it should not erase empty-space intent. SWOF therefore limits pass targeting to teammates in the same narrow eight-way intent sector and within a practical assistance range. If no teammate qualifies, the pass follows the stored facing and control does not jump to an unrelated player.

Literal 45-degree shot lines are too coarse near a 7.32-unit goal, so SWOF adds a bounded modern interpretation in the final approach. When an outfield player is close enough and faces credibly toward goal, the three goalward digital directions express upper-side, center, or lower-side intent. A projected miss is brought just inside the corresponding post; an already goal-bound line is retained. Long-range kicks and directions aimed away from goal are not corrected. Runner momentum is compensated at shot contact so it cannot silently bend the selected lane, and normal post-contact aftertouch remains available.

This is an original accessibility and control-resolution adaptation, not a claim about hidden SWOS code or constants.

This deliberately modernizes contact timing. It preserves the readable tap-versus-hold grammar and post-contact control of SWOS without copying unknown original thresholds, forcing key release, or applying physically impossible acceleration after the ball has left the foot.

## Follow-up behavior not yet implemented

- Airborne header/volley selection
- Eight-way, hold-powered tackle/header deflections
- Full playable throw-ins, corners, goal kicks, and penalties
- Triple-direction manager-bench shortcut

Those should reuse the same action state machine rather than introduce extra gameplay buttons.
