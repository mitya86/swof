---
name: swof-gameplay-ai
description: Use when designing, implementing, reviewing, or debugging SWOF futsal AI, tactics, coach behavior, difficulty, possession phases, player switching, goalkeeper ownership, or local/network multiplayer control assignment. Trigger for changes under Swof.Engine/AI, MatchEngine control handoffs, PlayerControlFrame, or AI behavior scenarios. Do not use for presentation-only feel problems or generic .NET work.
---

# SWOF Gameplay AI

Build AI as independently evaluated footballers executing ordinary player inputs inside a deterministic 5v5 futsal simulation. Add one observable pattern/instruction slice at a time and preserve human, AI, replay, and future network-controller parity.

## Start With Evidence

1. Run `bd prime`, inspect the relevant issue, and preserve unrelated worktree changes.
2. Read [architecture.md](references/architecture.md) completely.
3. Trace the current behavior through `Swof.Engine/AI`, `MatchEngine`, `PlayerControlFrame`, and focused scenarios in `Swof.Engine.Tests/Program.cs`.
4. Label each statement as current behavior, decided target behavior, or a new proposal. Do not present a target multiplayer or coach feature as implemented.

## Preserve The Core Model

- Treat 5v5 futsal as the foundation: four outfielders plus one goalkeeper per team on a 40 m by 20 m court.
- Give every computer-controlled outfielder its own `ComputerPlayerBrain` and immutable `PlayerDecisionContext`.
- Let patterns recognize facts and instructions decide whether they are executable. Return `null` when an instruction cannot run.
- Compare urgency bands before tactical scores. Keep registration order as the deterministic final tie-breaker.
- Emit normal `PlayerInput` through `PlayerControlFrame`. Never give AI a privileged movement, tackle, interception, or ball-control path.
- Let team tactics reweight individual executable choices. Do not replace the players with a single omniscient team brain.
- Change difficulty through perception, reaction, choice quality, anticipation, communication, and error, not through superior physics.

## Add One Behavior Slice

For each new pattern or instruction:

1. State the player-observable cue and the intended futsal behavior in one sentence.
2. Identify the match phase: own possession, opponent possession, loose ball, transition, restart, or goalkeeper distribution.
3. Implement a pure recognizer over the snapshot and player identity.
4. Implement an instruction that returns a stable kind, urgency band, situational score, and ordinary input.
5. Register it explicitly in the default brain.
6. Add deterministic scenarios for recognition, rejection, competition with adjacent instructions, stable ties, and exclusion of human-assigned slots.
7. Exercise the behavior in a live two-team match. A locally correct instruction can still be unreadable or conflict with control ownership.

Prefer a narrow end-to-end behavior over a taxonomy of unused abstractions. Keep tunable thresholds centralized and name them after the player-facing behavior they control.

## Handle Possession And Control Together

Possession phase, input ownership, and presentation ownership are related but distinct. Whenever possession can change:

- update `Ball.PossessorId` and `HasPossession` consistently;
- update `ActivePlayerId` when human control should follow the new owner or selected defender;
- ensure `ComputerPlayerController.CreateControlFrame` does not leave the human-reserved slot inert;
- test both teams gaining possession, passes with and without a valid receiver, interceptions, tackles, and goalkeeper collection;
- keep the active player and ball readable in the camera and marker presentation.

For future multiplayer, put every local, remote, and AI assignment into the deterministic per-tick control frame. Use the relay and goalkeeper rules in the reference; do not encode controller ownership inside a player brain.

## Add Tactics, Coaches, And Difficulty Safely

- Use `TacticalPriorities` to modify scores within a priority band. An emergency must not lose to a tactical preference.
- Let a coach observe aggregate match patterns at a coarser cadence, choose a bounded tactical profile, and communicate it with delay or imperfect adoption. The player only needs its reordered instruction priorities.
- Model stronger coaches as better timing, diagnosis, and communication. Model stronger players as better perception and execution of received instructions.
- Seed every stochastic decision and include the seed or resulting choice in replay/network state.
- Expose the selected instruction, score, and phase in diagnostic tooling before adding opaque sophistication.

## Validate And Hand Off

Use `$swof-validation` after implementation. Report:

- the new recognized pattern and emitted input;
- which human/AI/control transitions were tested;
- objective checks that passed;
- any live-play judgment still awaiting the user.

Do not close feel-dependent pacing, control, or readability work solely because deterministic tests pass.
