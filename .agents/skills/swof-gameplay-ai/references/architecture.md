# Gameplay AI Architecture And Decisions

Read this reference before changing SWOF AI, tactics, control assignment, or multiplayer behavior. It distinguishes working code from durable design decisions that are not implemented yet.

## Current Implemented Foundation

- The match is 5v5 futsal: four outfielders and one goalkeeper per team on a 40 m by 20 m court.
- `ComputerPlayerController` owns one `ComputerPlayerBrain` per eligible outfielder. Each brain evaluates the same immutable pre-tick snapshot for its own player.
- `IPlayerPattern<T>` recognizes reusable field situations. `IPlayerInstruction` turns a recognized situation into an executable `PlayerInstructionCandidate` or returns `null`.
- `ComputerPlayerBrain.Decide` compares `PlayerInstructionPriorityBand` first, then situational score plus `TacticalPriorities`, then stable registration order.
- Registered behavior covers loose-ball recovery, possession retention, ball-carrier pressure, cover, safe pass support, structured marking, and team-shape maintenance.
- AI output and assigned human input meet in `PlayerControlFrame`; `MatchEngine` consumes one deterministic frame per tick.
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
