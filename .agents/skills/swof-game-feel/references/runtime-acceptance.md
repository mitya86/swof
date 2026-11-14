# Runtime And Game-Feel Acceptance

## Evidence Hierarchy

Use the strongest evidence available for the claim:

1. deterministic behavior scenarios for simulation and control invariants;
2. build, format, and startup smoke checks for integration faults;
3. captured gameplay/video for motion and contact timing;
4. real-device play for touch, haptics, scale, readability, and comfort;
5. the user's hands-on judgment for “smooth,” “readable,” “connected,” and “fun.”

Passing a lower layer does not establish a higher-layer claim. Keep subjective work open when user confirmation is pending.

## Known Contracts And Lessons

- The real pitch is 40 m by 20 m. When it feels too small, investigate camera zoom, margins, framing, sprite scale, and movement tempo before changing law-scale dimensions.
- MonoGame is the primary playable host. WPF is retained reference/tuning tooling.
- Presentation uses VSync-limited variable rendering over a deterministic fixed-tick engine through `MatchEngine.Step(deltaSeconds, input)` and `GetRenderSnapshot()`.
- Cap large presentation deltas at 50 ms. Use frame-rate-independent camera damping and floating-point pitch transforms.
- Excessive velocity lookahead and integer source-rectangle rounding produced visible jumps. Do not reintroduce them.
- `PitchSurface` shares a sprite batch. Call `Prepare` before the gameplay batch begins; otherwise MonoGame throws “Begin cannot be called again until End has been successfully called.”
- Keep pixel-ish original sprites crisp with point sampling, but draw them at floating-point positions. Pixel art is an aesthetic, not permission for quantized motion.
- Keep the active player marker prominent and frame the ball plus active defender on opponent possession. Invisible ownership feels like inactive AI.
- Current readable-tempo defaults include outfield `14f / 60f`, goalkeeper `12f / 60f`, ground pass `20f / 60f`, and normal AI circulation delay 48 ticks. These are current tuning, not sacred constants.
- `SWOF/physics-tuning.json` under the OS local application-data folder (`%LOCALAPPDATA%` on Windows) can override code defaults. `CurrentProfileVersion` rejects stale profiles; inspect the loaded profile whenever a tuning change seems absent.

## Player Connection Priorities

Prioritize a coherent authored loop before broad rule expansion:

- immediate visible acknowledgement of accepted input;
- contact pose, ball launch, feedback event, sound, and optional haptic on the same authoritative tick;
- clear physical consequence from the actual trajectory and collision;
- nearby teammate/opponent reaction to the new situation;
- brief stable authorship through shirt number, name, role, or control marker.

The current research source is `docs/research/player-connection-and-game-feel.md`. Retain zero/low feedback options, avoid broad flashes or constant shake, and keep critical information available without sound.

## Suggested Play Scenarios

- Run straight, reverse at speed, release input, and turn while dribbling.
- Tap-pass to an aligned receiver, pass into space, and attempt a pass with no eligible receiver.
- Hold to shoot, then apply each aftertouch direction immediately after contact.
- Lose possession and verify the selected defender, teammate cover, distinct marks, marker, and camera.
- Let both AI sides circulate the ball and assess spacing and tempo.
- Compare `Dry`, `Connected`, and `Arcade` with the same deterministic pass/shot demo.
- On touch hardware, test rapid taps, borderline tap/hold duration, interrupted touches, safe-area edges, and simultaneous movement/action.
