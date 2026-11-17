# What the game draws today

An inventory of everything the MonoGame host puts on screen, written on 16 Nov 2026 as the starting point for the graphics rework. It describes the code as it is; it proposes no new look. It was read from the source, with a handful of captured frames; nobody has checked each item by eye in play.

Scope: `Swof.Game` (`MatchRenderer.cs`, `PitchSurface.cs`, `RenderPrimitives.cs`, `WorldCamera.cs`, `MatchFeedbackPresentation.cs`, `SwofGame.Draw`). `Swof.Droid` compiles the same files and the same content, so this is also what a phone draws. The WPF host and sound are out of scope.

## 1. The short version

- **Three image files are all the art there is**: one font (Arial 16) and two tiny player sheets (24 x 32 pixel frames, one standing frame and four running frames per team). Everything else — the court, the ball, shadows, rings, effects, the HUD, the menus — is drawn in code from a white pixel, a disc and a ring.
- **Players are one top-down figure rotated to face their direction.** There are two states, standing and running. A kick, a tackle, a slide, a save and a celebration have no frames; a kick is a squash and a nudge of the same sprite.
- **Team colours are painted into the sheets** (Home red, Away blue). Goalkeepers are the same sprite tinted. No player looks different from a team-mate: no number, hair or build.
- **One font at one size** does the score, the banners, the menus and the name tag.
- **No shaders, no depth, no lighting.** Four sprite batches, drawn in call order.

## 2. One frame, back to front

`SwofGame.Draw` takes the interpolated snapshot, turns it for the second half (`AsSeen()`), and calls `MatchRenderer.Draw`. The game view is the window minus a HUD bar at the top (44 px, or 62 with the debug HUD) and a 10 px border.

| # | Layer | Made of | Notes |
|---|---|---|---|
| 0 | Clear | colour (11,12,15) | Shows as the border round the game view |
| 1 | Pitch | one cached texture | Painted once in code, see section 4 |
| 2 | Players | per player, sorted by Y: possession ring, active ring and chevron, shadow, body sprite | The only layer that uses the image sheets. Point-sampled |
| 3 | Ball | shadow, black outline disc, near-white disc, dark spot | Always above every player. Height lifts it up the screen and grows it up to 50% |
| 4 | Feedback effects | lines, rings, small squares, discs | Section 6 |
| 5 | Active player's name tag | dark box and text at 0.62 scale | 17 px below the player |
| 6 | Line along the top of the game view | 2 px black | |
| 7 | HUD bar | rectangle (16,18,22,235) | Full window width |
| 8 | HUD text | `F0  HOME  1  -  0  AWAY  F0`, clock `mm:ss`, active player's number and name | Section 7 |
| 9 | Notice | dark box, yellow text | Short messages from the host, one at a time |
| 10 | Restart banner | dark box, yellow text | While the ball is out of play |
| 11 | Touch stick | ring and knob, translucent white | Only while a finger holds it |
| 12 | Touch button mark | ring | Only while a finger holds it |
| 13 | Pause button, or the open menu | square with two bars; or a dim over everything, a heading and one box per item | The match keeps being drawn under an open menu, including under the start screen |

Layers 1 to 6 are clipped to the game view. Officials exist in the snapshot and are never drawn.

## 3. The art files

| File | Size | What it is | How it is cut |
|---|---|---|---|
| `Content/Default.spritefont` | Arial 16, ASCII 32 to 126 | The only font | A character outside that range has no glyph; names must stay ASCII today |
| `Content/PlayerAtlasV2.png` | 48 x 32 | Standing figure | 2 columns: Home, Away. One frame each |
| `Content/PlayerRunAtlas.png` | 96 x 64 | Running figure | 4 columns of run frames, 2 rows: Home, Away. Frames 2 and 3 are drawn mirrored |
| `Content/PlayerAtlasV2.source.png`, `PlayerRunAtlas.source.png` | 820 x 480, 1639 x 960 | Larger originals | Not built, not referenced anywhere. How the small sheets were made from them is not recorded |
| `Icon.bmp`, `Icon.ico` | 256 x 256 | Window and app icon | Not drawn in the game |

Made in code when the game starts: a 1 x 1 white pixel (every rectangle and line), a 64 px hard-edged disc, a 64 px hard-edged ring (its thickness grows with the radius it is drawn at), and the pitch texture.

**How a player is drawn.** Running if speed is above 4% of a reference speed, otherwise standing. The frame is rotated so the head points along a blend of where the player is moving and where it faces. The run cycle advances at 8 to 14 frames a second with speed, on the renderer's own clock. On top of the frame: a small forward lean, a sway and a bob while running; winding up a kick pulls the sprite back and squashes it; the kick itself pushes it forward and stretches it. Sprite height is 3.4 player radii, which at the default window is about 113 px from a 32 px frame, an enlargement of about three and a half times, so the pixels are uneven. Goalkeeper tints: Home (145,255,175), Away (255,220,130).

## 4. The pitch

Painted once into a 2048 px wide texture (court plus 3 m all round, about 44 px per metre), then drawn as one image. At a 1280 x 720 window it is stretched about 1.4 times, and more in a larger window. The texture cannot simply be made larger: the graphics profile in use caps a texture at 2048 px.

In paint order: surround (17,25,32); apron 0.7 m wide (24,77,98); court (35,123,157); eight panels across the length, alternate ones (32,116,149); two goal nets (translucent fill, back and side lines, four mesh lines); two benches along the top side only (42,47,58) with a roof strip (78,88,108); then the lines in (242,242,242), 8 cm wide: boundary, halfway line, centre circle and spot, the two penalty areas, penalty marks at 6 m and 10 m with the two small marks 5 m either side, corner arcs, four substitution ticks on the top touchline, and both goal lines between the posts at double width.

The pitch is not turned at half-time, so benches and substitution ticks stay on the top side in both halves.

## 5. The camera

A rectangle in metres, no rotation, no tilt. It shows half of the court's length (20 m of 40) and whatever height the window's shape gives; it zooms out when needed to keep both the ball and the active player in view with 2.25 m to spare. Its centre is 72% ball, 28% active player, pushed ahead of the ball by 14 ticks of the ball's velocity (capped at 24% of the view), and held inside the court plus 3 m. It follows its target with a smoothing rate of 8 a second. At the default window that is 63 px per metre: a player's body (0.53 m) is about 33 px in radius and the ball (0.21 m) 13 px. Until 17 Nov 2026 bodies were twice that size and the view 28 m.

One effect exists: a 7 cm push away from a shot or a goalkeeper's catch, fading over 0.18 s. It is off unless F4 turns it on, and no launch option does.

## 6. Feedback effects

The engine raises seven events; each becomes a short burst that lives 0.72 s. Three modes: **Dry** draws none of this, **Connected** (the default) the modest version, **Arcade** the larger one. F2 cycles them, `--feedback=` sets one. Sizes are screen pixels and do not scale with zoom.

| Event | Connected | Arcade adds |
|---|---|---|
| Button accepted | growing ring, pale yellow | bigger ring |
| Pass struck | short line along the pass, a cross line, five drifting specks, pale blue; starts the ball's trail | 1.1x size |
| Shot struck | the same in orange; starts the trail | 1.45x size |
| Possession changed | growing ring, pale green | bigger ring |
| Tackle started | short line along the slide, pale blue | longer, thicker, two angled lines |
| Goalkeeper's catch | ring in cyan and a cross line | a second, wider white ring |
| Goal | large growing ring, yellow | eight orange rays |

**Ball trail.** After a pass or shot, a line follows the ball until someone takes it, play stops, or the ball slows. Connected: one thin pale line, 0.22 s long. Arcade pass: a wide blue line under a thin pale one with white specks, 0.78 s. Arcade shot: three stacked lines from dark red to yellow, plus a **fireball**: three glowing discs and nine specks drawn over the ball, which hide the ball itself.

**Players react.** For 0.34 s after a pass, shot, change of possession, tackle or catch, the other players within 18 m turn part of the way toward it and lean a little.

Always on, whatever the mode: the white ring on the player with the ball, the yellow ring, chevron and name tag on the active player, the wind-up squash and kick stretch.

## 7. Words on screen

All text is built in code; there is no table of strings to translate.

- **HUD**: fouls, `HOME`, score, `AWAY`, fouls; the clock; the active player's number and name.
- **Banner**: `HALF-TIME`, `GOAL`, `KICK-IN`, `CORNER`, `FREE KICK`, `PENALTY`, `10 M KICK`, `GOAL CLEARANCE`, with ` - HOME` or ` - AWAY`, and `FOUR SECONDS` in front when the count ran out.
- **Menus** (from `Swof.Engine/MatchFlow.cs`): heading `SWOF`, `Paused`, or `Full time   Home 2 - 1 Away`; items `Play`, `Difficulty: normal`, `Resume`, `Tactic: high press` (and `half court`, `push up`, with ` (on)`), `Leave match`, `New match`.
- **Notices**: tactic changed, game feel mode, fullscreen, debug HUD, camera feedback, tuning reloaded, audio unavailable, and once at launch `Both teams active: support runs and structured marking`.
- **Debug HUD** (F3): two lines of state and a key guide.

## 8. Where a new look plugs in

- **One door.** `MatchRenderer.Draw(snapshot, font, …, settings, touchControls, pauseMenu, selectedItem, heading)` is called from one place. A new renderer that takes the same things can replace it without touching the engine.
- **What it is given.** The snapshot (court, players, ball, scores, fouls, clock, last event, feedback events), the touch controls' current stick and button points, and the menu's labels and boxes.
- **Layout the engine owns.** The pause button, the menu boxes and the stick radius are sized in `Swof.Engine/PauseMenu.cs` and `TouchControls.cs` as fractions of the screen, because touches are tested against the same boxes. Moving them means changing them there, not in the renderer.
- **Time lives in the drawing.** Effects age, the camera settles and run cycles advance inside `Draw`, on real frame time. They keep moving while a menu is open and the match is frozen.
- **The renderer decides some presentation itself**: which banner text fits the last event, the HUD's wording and clock, standing versus running, and when a ball trail ends.
- **It remembers between frames**: the smoothed camera, each player's run phase, the bursts and the trail. None of it is reset for a new match except by the effect code noticing the clock went backwards.

## 9. What the snapshot knows that nothing draws

Officials, the period, each player's futsal role, the last event's place and its sentence (for example "Free kick to Home."), and the strength of each feedback event.

## 10. Oddities to settle during the rework

Left as they are on purpose, so the rework decides them once:

1. **The font is Arial**, taken from the build machine. It is why the Linux package is built on a Mac, and whether the project may ship it as a file has not been established. A font the project is known to be free to bundle removes both problems.
2. **The full-time banner still says `R OR START: NEW MATCH`**, which no longer does anything. In normal play the full-time menu hides it; it shows only in the scripted demo modes.
3. **`PLAY STOPPED`** is a banner nothing produces.
4. **The opening notice** about support runs is a developer's note shown to every player when the game opens.
5. **Translucent colours are written one way and blended another** (plain alpha values into a premultiplied blend), so light translucent items such as the touch stick and nets come out brighter than their numbers say.
6. **Nothing accounts for a phone's notch or rounded corners.** The HUD row starts 14 px from the left edge and the pause button sits 2% from the top right.
7. **The pitch texture is one size.** On a screen wider than about 2000 px the lines soften.
8. **The fireball covers the ball** in Arcade mode.
9. **The two large source images** are tracked but unused, with no record of how the small sheets came from them.
