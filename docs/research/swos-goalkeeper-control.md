# Goalkeeper Control And Match-Person Model

This is a clean-room behavior contract. It records the control boundary observed in
the Sensible manuals, then defines a modern deterministic implementation without
copying original code or data.

## Evidence

The original *Sensible Soccer* manual gives the clearest goalkeeper rule: keepers
are computer-controlled during normal play. The user only takes over the release
when the keeper holds the ball or is preparing a goal kick, and even then the
available action is to kick the ball back into play.

The *Sensible World of Soccer 96/97* manual is consistent with that minimal model:
the user normally controls one team member at a time, passing chooses a teammate in
the facing direction, and goal kicks use the same directional one-button kick model
as normal play.

Sources:

- https://www.lemonamiga.com/doc/sensible-soccer-v1-0/1415
- https://www.abandonwaredos.com/docawd.php?idg=3299&sf=sensibleworldofsoccermanual.txt&sg=Sensible+World+of+Soccer+96%2F97&st=manual

## Match-Person Architecture

Spatial state shared by every person on or around the pitch is exposed through
`IMatchPersonState`. Footballers and officials remain separate concrete types so
capabilities are granted by type and role instead of scattered numeric ID checks.

| Person | State and role | Team | Ball possession | Direct user control |
| --- | --- | --- | --- | --- |
| Field player | `PlayerState`, `PlayerRole.Outfield` | Yes | Feet | Normal active-player control |
| Goalkeeper | `PlayerState`, `PlayerRole.Goalkeeper` | Yes | Hands in own penalty area; distribution | AI off-ball; user aims/releases held ball only |
| Referee | `OfficialState`, `MatchOfficialRole.Referee` | No | Never | Never |
| Other officials | `OfficialState`, assistant/fourth-official roles | No | Never | Never |

`MatchState.Players` is therefore the team-footballer collection and
`MatchState.Officials` is the non-team official collection. This prevents an
official from entering possession, selection, passing, tackling, or team queries by
accident. `PlayerRole` handles differences within the footballer rules.

Goalkeeping skills also live in `GoalkeeperAttributes` rather than overloading the
outfield skill record. Handling and reflexes affect catch reach; positioning and
reflexes affect the AI guard response. This leaves room for diving, parrying, and
distribution accuracy without inventing goalkeeper meanings for heading or
tackling values.

## Implemented Tick Contract

1. The active outfield player consumes direct movement input.
2. Off-ball goalkeepers independently choose a guard point from ball position,
   then move inside a hard keeper zone near their own goal.
3. A loose ball advances through its normal physics step.
4. Keepers test the swept ball segment, not only its final tick position. An
   eligible fast shot therefore cannot tunnel through a keeper between 60 Hz ticks.
5. A caught ball changes to `BallControlKind.GoalkeeperHands`; foot-dribble and
   passive-tackle rules no longer run for it.
6. A user-team catch temporarily selects the keeper. Direction aims the release but
   cannot move the keeper. Tap passes to an outfield teammate; hold kicks long.
7. After release, selection returns to an outfield player. An opposing keeper never
   steals user selection and distributes after a deterministic AI delay.

The renderer uses the same pixel footballer atlas and smooth interpolated positions
for all footballers, with a distinct keeper tint. That keeps visual scale and motion
coherent while making the role immediately readable.

## Deliberate First-Slice Limits

- Saves currently resolve as catches. Parry direction, spills, dives, animation
  choice, and shot-versus-hand outcome scoring are not implemented yet.
- The engine does not yet track the last deliberate touch, so the back-pass and
  direct-throw-in handling restrictions cannot be enforced correctly.
- Goal kicks are classified as restarts but are not yet playable restart states.
- The keeper guard target is positional and deterministic; it does not yet predict
  crossing trajectories, close down one-on-ones, or coordinate with defenders.
- Official state is architecturally separate, but referee movement, collision
  avoidance, fouls, advantage, and cards are later systems.
- Penalty kicks will need their own temporary goalkeeper input policy. Normal
  off-ball play will remain AI-controlled.

These are explicit extensions to the role/capability model, not reasons to weaken
the off-ball goalkeeper control boundary.
