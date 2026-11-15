# Gameplay AI Architecture And Decisions

Read this reference before changing SWOF AI, tactics, control assignment, or multiplayer behavior. It distinguishes working code from durable design decisions that are not implemented yet.

## Current Implemented Foundation

- The match is 5v5 futsal: four outfielders and one goalkeeper per team on a 40 m by 20 m court.
- `ComputerPlayerController` owns one `ComputerPlayerBrain` per eligible outfielder. Each brain evaluates the same immutable pre-tick snapshot for its own player.
- `IPlayerPattern<T>` recognizes reusable field situations. `IPlayerInstruction` turns a recognized situation into an executable `PlayerInstructionCandidate` or returns `null`.
- `ComputerPlayerBrain.Decide` compares `PlayerInstructionPriorityBand` first, then situational score plus `TacticalPriorities`, then stable registration order.
- Registered behavior covers loose-ball recovery, shooting, possession retention, ball-carrier pressure with a slide tackle, cover, safe pass support, structured marking, and team-shape maintenance.
- A computer shot is the same held button a human uses. The hold is returned in the `Urgent` band until the ball leaves, and the stick stays neutral through the aftertouch window, so computer players never bend or lift a shot.
- The presser shadows a carrier within reach for a third of a second, then presses once to slide. There is no other tackle: releasing the button without the ball does nothing, for humans and computer players alike.
- A slide that reaches an opposing carrier along the way the carrier faces, with the ball beyond the slide's reach, is a foul: play stops and the carrier's team restarts with a free kick on the spot (`SlideFoulRule`, `PhysicsTuning.SlideFoulBehindDot`; above 1 switches it off). The rule travels in the snapshot, and the presser asks the same rule before pressing, so against a carrier who shields the ball it steps round to the ball and takes it on its feet (holding station instead let a person keep the ball for ever by standing still). It assumes the carrier stands still during the slide, so computers still foul about once a match. Nothing else is a foul.
- `TeamTactic` is per-team data set with `ComputerPlayerController.SetTactic` and read by patterns from `PlayerDecisionContext.Tactic`. It holds two numbers. `PressLineMetres`: beyond that distance from its own goal line a team's presser and cover wait on the line instead of following the carrier (`DefensiveAssignments.PressPoint`). `MarkersUpMetres`: how much further from their own goal the two marking players stand and may go; at 0 they stay within a per-role depth of 9 to 15 m whatever the press does (`StructuredMarkingPattern`). Presets: `HighPress` (the default: whole court, markers at 0), `HalfCourt` (line at 20 m) and `PushUp` (whole court, markers 6 m up). In computer-versus-computer matches pushing the markers up is simply stronger, not a trade, and at 12 m for both teams the match misses the pass marks. Where team-mates stand in possession (`OpenPassingOptionPattern.GetRoleOffset`) was tried as tactic data and dropped: moving the alas along the court made a team clearly worse, and wider alas or a further pivo changed nothing measurable. No coach or menu chooses a tactic. `TacticByScore` (off unless `ComputerPlayerController.SetTacticByScore` is called) swaps a team's tactic from the snapshot alone: push up while losing, half court while winning in the last quarter. It is a scoreboard rule, not the decided coach. Over 300 seeds a setting, results better to worse for the team given it, seed by seed against the same match without the rule: pushing up when behind, Away 116 to 66 but Home 104 to 92; waiting when ahead, Away 47 to 27 but Home 27 to 21. So it helps Away modestly and Home not measurably (why the sides differ is not known), and far less than always pushing up does. In `Swof.Game` a person can cycle their own team's with T or gamepad Y, or pick it in the pause menu (`PauseMenu` beside `TouchControls`: P or a touch on the top right corner; its touches never reach the stick or the button) (`--team-tactic=` at launch); the person's player is always the presser, so on their team half court moves only the cover and push up moves the two markers.
- AI output and assigned human input meet in `PlayerControlFrame`; `MatchEngine` consumes one deterministic frame per tick.
- `MatchEngine.UserControlledTeam` is null when computers play both teams. Then there is no active player (`ActivePlayerId` 0), both goalkeepers distribute on the computer delay and each team picks its presser by position. Balance figures from the match runner use this mode; with a user team the engine deliberately treats that side differently.
- Chance comes only from `DecisionNoise.Roll(match, playerId, purpose, holdTicks)`, a hash of the match seed (`MatchState.Seed`), tick and player. Use it for computer error (so far: carry direction leans differently twice a second). Never use `System.Random` in the engine, and never put chance in physics.
- The user controls an outfielder selected by possession/defensive handoff. The goalkeeper becomes user-controlled only while holding the ball for distribution.
- Defensive handoff selects a deterministic nearby goal-side outfielder. AI teammates then cover and mark while the human slot remains reserved.

## Decided Direction, Not Yet A Completed Feature

### Multiplayer ownership

- Maximum human count is eight: four outfield controllers per side. Goalkeepers remain AI-controlled except when their team goalkeeper possesses the ball.
- With one to three humans on a side, users do not own permanent player identities. A completed targeted pass relays control of the receiver to the next user in the side's deterministic rotation.
- With four humans on one side, each human has a stable outfield assignment because all outfield slots are occupied.
- Controller assignment belongs above player AI. It must be deterministic, replayable, and represented in the same per-tick input frame used by local humans and AI.
- Network play will require synchronized inputs, controller-assignment events, seeded randomness, and a clear rollback/lockstep policy. Do not hide network state inside instructions.

### Tactics and coach

- Players do not need a symbolic understanding of formations or tactics. They evaluate prioritized executable instructions from their local context.
- Team tactics adjust relative instruction scores within urgency bands. For example, a conservative profile can favor a nearby safe receiver; an attacking profile can favor a through-space opportunity when it is actually recognized.
- A computer coach is a slower aggregate decision layer, not a second real-time controller. It observes trends, changes a bounded tactical profile, and waits before reconsidering.
- Coach quality affects diagnosis, timing, and clarity. Player quality affects perception and how quickly or accurately commands are adopted. Emergency player behavior remains available regardless of coach preference.

### Difficulty

Increase difficulty through:

- shorter but non-zero reaction delay;
- better estimates of pressure, passing lanes, and future space;
- less decision noise and fewer false pattern recognitions;
- better anticipation and coordinated timing;
- better tactical adaptation and command adoption.

Do not increase maximum speed, tackle reach, ball attraction, interception radius, or other physical rules only for the AI. Fairness and replayability depend on shared mechanics.

## State And Phase Rules

- Determine phase dynamically from authoritative possession on every decision frame: own possession, opponent possession, loose ball, transition, restart, or goalkeeper distribution.
- `ActivePlayerId` means user input/presentation ownership. It is not always the same concept as ball possession.
- A phase-recognition fix is incomplete if the active human slot is stale, excluded from AI, off-screen, or visually ambiguous.
- Use pressure, cover, and distinct goal-side marks instead of making every defender chase the ball.
- Keep futsal spacing compact but avoid identical destinations. Each off-ball player needs a distinct support, width, depth, cover, or marking responsibility.

## Clean-Room SWOS Boundary

Use SWOS as a behavioral reference for immediacy, one-button grammar, facing, and readable arcade feel. Do not copy code, binary constants, sprites, or unverified timing. Record whether evidence came from a manual, observation, measurement, or a new SWOF design choice.

## Common Failure Patterns

- **AI appears inactive:** inspect `ActivePlayerId`, reserved human inputs, camera framing, and marker visibility before rewriting phase logic.
- **Default tuning changes have no effect:** inspect `SWOF/physics-tuning.json` under the OS local application-data folder (`%LOCALAPPDATA%` on Windows) and `PhysicsTuning.CurrentProfileVersion`.
- **A pass changes the wrong user/player:** distinguish target selection, ball possession, active-player ownership, and multiplayer controller rotation.
- **Every defender converges:** add deterministic role assignment before adjusting steering constants.
- **Difficulty feels unfair:** find physical or information advantages and move the distinction into perception/decision timing.
