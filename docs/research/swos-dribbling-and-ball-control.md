# SWOS Dribbling And Ball Control Notes

## Summary

The current evidence supports the theory that SWOS treats dribbling as loose ball control rather than gluing the ball to the player's feet.

Confirmed from local data and community reverse-engineering:

- SWOS stores an explicit `Ball_Control` attribute per outfield player.
- Player attributes are packed as nibble values in the order passing, shot velocity, heading, tackling, ball control, speed, finishing.
- The visible 0-15 range is functionally an 0-7 gameplay scale for many purposes: `0` mirrors `8`, `1` mirrors `9`, and so on.
- Pitch-condition notes from the SWOS community specifically say frozen and muddy pitches make ball control worse, which implies ball control is part of live match behavior, not just menu flavor.

Observed/manual behavior also supports the same model:

- The ball is not stuck to the controlled player.
- Fast running makes it harder to change direction and retain control.
- Better control players are harder to separate from the ball.

Not confirmed yet:

- The exact in-match dribble/touch formula.
- Whether SWOS advances ball touches on animation steps, fixed timers, speed thresholds, or small lookup tables.
- Whether possession loss is deterministic thresholding, randomness, or both.

## Local Binary And Data Evidence

Main executable:

```text
C:\games\swos_96_97\SensiWs9\SOC\SWS.EXE
```

String scan finds the menu/stat labels:

```text
PASSING
SHOOTING
HEADING
TACKLING
BALL CONTROL
SPEED
FINISHING
FITNESS
```

Team data lives in files such as:

```text
C:\games\swos_96_97\SensiWs9\SOC\DATA\TEAM.000
```

`TEAM.000` is 10,946 bytes. The first two bytes encode a team count of 16, followed by 16 team blocks of 684 bytes:

```text
(10946 - 2) / 16 = 684
```

The team block layout matches the published SWOS team structure:

- team header and kit data
- coach name
- 16-player order table
- 16 player records

Each player record is 38 bytes. The editor source for `AG_SWSEdt` decodes the five skill/value bytes like this:

```text
byte 28: passing in low nibble
byte 29: shot velocity in high nibble, heading in low nibble
byte 30: tackling in high nibble, ball control in low nibble
byte 31: speed in high nibble, finishing in low nibble
byte 32: transfer value code
```

Example decoded from `DATA\TEAM.000`, first team:

```text
ARIAN HASA:         control 11, speed 9
BRIKELDO SHABANI:  control 8,  speed 9
```

A scan of the local database finds many high-control/high-speed players, which is exactly the kind of pairing expected for good dribblers:

```text
FREDDY RINCON        COLOMBIA          control 15, speed 15
ROMARIO              VALENCIA          control 15, speed 15
DENNIS BERGKAMP      HOLLAND           control 15, speed 15
ALESSANDRO DEL PIERO ITALY             control 15, speed 15
```

## Design Implications For SWOF

We should stop treating possession as "ball position equals player position plus a fixed facing offset."

The better model is:

```text
Player has influence over the ball while it is inside a control envelope.
The ball remains a real physical object with its own position and velocity.
Dribbling is a sequence of small touches.
Each touch pushes the ball in front of the player based on facing, velocity, and current input.
Low control, high speed, sharp turns, and bad pitch conditions increase touch distance and loss risk.
```

Useful state to add:

```text
PlayerAttributes.BallControl
PlayerAttributes.Speed
DribbleTouchCooldownTicks
LastDribbleTouchTick
PossessionOwnerId or ControlledByPlayerId
Ball.ControlState: free, controlled, contested
```

Deterministic first-pass rules:

- Normalize SWOS control to a 0-7 effective value with `effective = raw % 8`.
- Let high control shorten the time between clean touches.
- Let high speed improve running speed but make ball retention more demanding.
- Compute a turn penalty from the angle between current facing, desired movement, and ball direction.
- Detach the ball when it leaves the control envelope or gets too far to the side/behind the player.

Initial tuning shape:

```text
touchIntervalTicks = lerp(10, 5, control01)
baseLeadDistance = lerp(1.1m, 0.7m, control01)
speedLead = playerSpeed01 * lerp(1.2m, 0.45m, control01)
turnPenalty = turnAngle01 * playerSpeed01 * lerp(1.2m, 0.35m, control01)
maxControlDistance = lerp(1.25m, 2.0m, control01)
```

This keeps the rule deterministic and tunable while matching the important SWOS feeling: sprinting in straight lines is possible, abrupt turns are risky, and excellent ball-control players can change direction with less separation.

## Follow-Up Research

To get closer to original SWOS, the next research steps are:

- Record short gameplay clips at fixed emulator speed and measure player/ball pixel separation during straight runs and 90-degree turns.
- Compare high-control and low-control players from the decoded data.
- Test muddy/frozen pitch conditions and measure whether the ball moves farther per touch or whether the control envelope shrinks.
- Disassemble the match loop around references to the decoded player skill bytes if we decide exactness is worth the time.

## References

- SWOES team structure notes: https://swoes.info/team_structure/
- AG_SWSEdt source, `TSWSPlayer.LoadPlayer`: https://github.com/anoxic83/AG_SWSEdt/blob/master/src/uagswos.pas
- SWOS United value discussion: https://swosunited.com/forum/editing-programming/19133-how-swos-compute-players-price-value
- SWOS 96/97 pitch notes: https://www.sensiblesoccer.de/media/kunena/attachments/Pitch%20types%20in%20SWOS%2096_97%20Amiga%20-%20version%201.0%20-%20January%202018.pdf
- International Sensible Soccer manual scan: https://www.gamesdatabase.org/Media/SYSTEM/Microsoft_DOS/manual/Formated/International_Sensible_Soccer.pdf
