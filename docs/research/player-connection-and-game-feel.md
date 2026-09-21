# Player Connection And Game Feel

This note treats the current feeling of emptiness as a perception-action problem,
not as a shortage of decorative effects. The goal is to make the player feel that
their input is understood, that footballers inhabit the match, and that the match
notices what just happened.

## Executive Finding

SWOF currently simulates actions more richly than it presents them. Input changes
authoritative state, but most actions produce only ball movement and a small sprite
pose change. There is almost no sound, transient visual confirmation, social
reaction, or persistent identity. The pitch therefore reads as a physics test with
tokens rather than a football match with people.

The highest-value fix is one coherent feedback loop:

```text
intent -> acknowledgement -> contact -> physical consequence -> social reaction -> memory
```

For a kick, that means: the player visibly commits as soon as the button is pressed;
contact, ball launch, sound, and a restrained pixel effect occur on the same tick;
nearby players respond to the new situation; the camera and crowd acknowledge the
importance without obscuring play; and the user knows which footballer authored the
moment.

This should be built and evaluated before expanding rules breadth.

## What Research Supports

### Responsiveness supports competence and agency

Ryan, Rigby, and Przybylski found across four studies that perceived autonomy and
competence are associated with enjoyment and future play. They also found that
intuitive controls support those needs and presence. Responsiveness is therefore not
only a technical latency concern: it contributes to the user's belief that an outcome
was theirs.

The full path matters. A 2007 *Game Developer* analysis shows how individually small
frame-ordering delays accumulate between input, simulation, rendering, and display.
Controlled latency studies in competitive games likewise find measurable performance
and experience degradation below 100 ms. The genre and task differ from SWOF, so the
exact thresholds should not be transplanted, but the direction is reliable: reduce
avoidable delay and make accepted input visible immediately.

For football animation, realism is not a sufficient answer. EA's own responsiveness
work describes the continuing trade-off between animation variety/naturalness and
consistent expected response. SWOF should let simulation determine consequence while
animation communicates it; animation must not postpone the action the user already
requested.

Sources:

- Ryan, Rigby, and Przybylski, *The Motivational Pull of Video Games*:
  https://doi.org/10.1007/s11031-006-9051-8
- *Programming Responsiveness*, Game Developer, September 2007, pp. 56-58:
  https://media.gdcvault.com/GD_Mag_Archives/GDM_September2007.pdf
- Liu et al., *The Effects of Network Latency on Counter-Strike: Global Offensive Players*:
  https://doi.org/10.1109/QoMEX55416.2022.9900915
- EA SPORTS FC gameplay responsiveness notes:
  https://www.ea.com/games/ea-sports-fc/fc-26/news/pitch-notes-fc26-gameplay-responsiveness-update

### Feedback helps, but indiscriminate spectacle does not

"Juice" is redundant audiovisual feedback: one player action creates several
non-functional but informative reactions. CHI PLAY research found that visual
embellishment consistently improved visual appeal, while changes to competence were
context-dependent. A larger study of 3,018 action-RPG players found that both no
feedback and extreme feedback underperformed medium/high feedback in play time,
experience, intrinsic motivation, and performance.

That rules out a generic checklist of shake, bloom, particles, and flashes. Effects
should be local, directional, brief, proportional, and semantically tied to an event.
The ball, players, and tactical space must remain readable.

Sources:

- Hicks et al., *Juicy Game Design: Understanding the Impact of Visual Embellishments on Player Experience*:
  https://doi.org/10.1145/3311350.3347171
- Kao, *The Effects of Juiciness in an Action RPG*:
  https://doi.org/10.1016/j.entcom.2020.100359
- Berbece, *Game Feel: Why Your Death Animation Sucks*, GDC Europe 2015:
  https://www.gdcvault.com/play/1022759/Game-Feel-Why-Your-Death

### Character sound is a connection channel, not background decoration

A two-experiment study of ambient and character sound found significant effects from
character sounds on immersion, avatar identification, and fun in its second
experiment. For SWOF, the first audio pass should therefore prioritize authored
action sounds—foot/ball contact, trap, slide, whistle, keeper catch, and player exertion—
before music. Crowd ambience should respond to threat and outcome rather than play as
an unrelated loop.

Source:

- Haehn, Schlittmeier, and Boffel, *Exploring the Impact of Ambient and Character Sounds on Player Experience in Video Games*:
  https://doi.org/10.3390/app14020583

### Identity needs embodiment and recognition

Research on game-character identification describes play as a temporary shift in
self-perception toward the controlled role. A responsive avatar supports that shift,
but SWOF currently offers generic labels, limited individual behavior, and no visible
history. Football also adds team attachment: a user should recognize their side's
shape, colors, players, and reactions at a glance.

Identity does not require large portraits during play. It can begin with stable shirt
numbers, short fictional surnames, distinct silhouettes or palettes, visible role,
and tiny player-specific tendencies. A name appearing briefly on control switch is
more useful than keeping a permanent debug label across the top of the screen.

Sources:

- Klimmt, Hefner, and Vorderer, *The Video Game Experience as True Identification*:
  https://doi.org/10.1111/j.1468-2885.2009.01347.x
- Przybylski, Rigby, and Ryan, *A Motivational Model of Video Game Engagement*:
  https://doi.org/10.1037/a0019440

## Audit Of The Current Build

### 1. The visible match is structurally sparse

The default state contains three outfield players and one goalkeeper per team on a
full 105 by 68 metre pitch. Only the active outfield player and goalkeepers move.
Other footballers do not support, close down, scan, call for the ball, or react to
contact. Even excellent sprites will look like placed tokens when the team has no
collective motion.

### 2. Input is simulated promptly but presented softly

The host polls input each presentation update and advances a deterministic 60 Hz
simulation. Rendering interpolates the previous and current snapshots. This is a good
foundation for smooth motion, but it can trade roughly one simulation tick of visible
directness for interpolation and it interpolates action pose values as well as
position. A short press also has no host-side input buffer if it begins and ends
between simulation ticks.

The fix is not to remove interpolation. Position should stay smooth, while discrete
intent and contact channels use the newest authoritative state. Button edges should be
buffered until consumed by a simulation tick.

### 3. Actions have no presentation event spine

The engine exposes out-of-play events only: goal, touchline out, and goal-line out.
Pass contact, shot contact, ball trap, tackle, possession change, keeper catch, and
save are inferred indirectly or not represented. Presentation therefore has no clean,
exactly-once trigger for effects, sound, haptics, crowd response, or reactions.

### 4. The game is silent

The MonoGame content manifest contains one font and two player atlases. There are no
match sound assets or audio systems. This removes the fastest confirmation channel for
contact and leaves the crowd, ball, players, whistle, and goal emotionally inert.

### 5. Animation shows locomotion, not intention or consequence

Players have a four-frame run cycle and subtle charge/follow-through deformation.
There is no readable plant foot, trap, tackle impact, stumble, turn anticipation,
keeper save response, idle weight shift, glance toward the ball, appeal, celebration,
or disappointment. The controlled character therefore moves but rarely acts as a
person.

### 6. Camera and environment observe rather than respond

Camera damping and lookahead produce smooth tracking, but there are no event-scaled
impulses or framing changes. Static crowd dots and a minimal goal do not respond to
attacks, saves, or scores. Smoothness is present; dramatic punctuation is absent.

### 7. Prototype instrumentation dominates the frame

The two-line HUD gives tick, FPS, ball owner, attributes, and all controls permanent
top billing. The active player is marked by concentric rings. These are useful debug
tools, but they frame the experience as software under test and compete with the tiny
footballer for attention.

## Target Connection Grammar

Every important football interaction should define the same six stages.

| Stage | Kick example | Presentation budget |
| --- | --- | --- |
| Intent | plant/lean begins when action is accepted | visible on next presented frame |
| Acknowledgement | compact charge cue and quiet cloth/step sound | immediate, low intensity |
| Contact | foot pose, ball launch, kick sound, 4-7 grass pixels | same authoritative tick |
| Consequence | short ball streak, receiver turn, defender reaction | follows real trajectory |
| Social reaction | support run, close-down, crowd lift | proportional to threat |
| Memory | author name/number, score/event history | brief and stable, not noisy |

Different actions reuse the grammar with different intensity. A simple trap should
not shake the screen. A driven shot may add a very small directional camera impulse
and short controller pulse. A goal can briefly widen the reaction radius and crowd
response, but gameplay readability remains dominant.

## Recommended First Vertical Slice

Build one complete pass/shot loop across engine and presentation before adding more
isolated systems.

1. Add an ordered, exactly-once gameplay feedback stream for action accepted, ball
   contact, possession change, tackle contact, keeper catch, and goal.
2. Buffer press/release edges until a simulation tick consumes them. Render current
   intent/contact state while continuing to interpolate positions.
3. At kick contact, align pose, ball launch, one restrained directional effect, one
   layered sound, and optional short haptic pulse on the same tick.
4. Make the nearest relevant teammates and opponents face, lean, or take one
   purposeful support/close-down step. This is presentation of awareness, not a full
   tactical AI project.
5. Replace the permanent debug HUD by default with a compact scorebug and a brief
   name/number marker on player switch. Keep debug data behind a toggle.
6. Add a feedback-intensity setting from zero to full. Camera shake and full-screen
   effects must be independently disableable.

After this slice feels good, add a populated team shape and real off-ball behavior.
Adding twenty static bodies first would make the screen crowded rather than alive.

## Evaluation Plan

Keep a dry mode and a connected mode available from the same build. Run the same
five-minute scenario in both modes and collect:

- input poll to first visible acknowledgement, target no more than one presented frame
- action-contact event to ball launch, target the same simulation tick
- duplicate or dropped feedback events, target zero in deterministic replay
- ratings from 1-7: responsive, clear, physically convincing, player attachment,
  team awareness, desire to continue
- missed quick taps and unintended actions
- effect intensity preference and any discomfort

Record gameplay video at 60 fps and inspect button/contact timing frame by frame. A
better-looking still is insufficient; the test is whether the action loop feels owned
when played repeatedly.

## Restraint And Accessibility

Do not use broad white flashes, repeated high-contrast pulses, constant camera bob,
or large shake. Provide a zero setting for camera and secondary motion effects and
keep important information available without sound. Microsoft's accessibility
guidance recommends making shake and other camera movement adjustable or removable
and minimizing photosensitivity risks.

Sources:

- Xbox Accessibility Guideline 117, visual distractions and motion:
  https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/117
- Xbox Accessibility Guideline 118, photosensitivity:
  https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/118

## Decision

Pause broad feature expansion long enough to build and playtest the first connection
slice. Goalkeeper depth, restarts, officiating, and tactical sophistication will all
benefit from the same event, input, reaction, audio, and identity spine. Without it,
each new rule risks becoming another correct state change that the player barely
feels.
