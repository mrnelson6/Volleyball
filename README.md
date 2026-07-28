# Volleyball — 2.5D 2v2 Pixel-Art Volleyball

A 2.5D pixel-art **2v2 beach volleyball** game built in Unity 6, targeting **web browsers
(WebGL)** and **mobile** (touch). Players move on a 3D court (X/Z ground plane) while the
ball arcs through 3D space (Y height) using real physics. Characters and the ball are
pixel-art sprites that billboard toward a tilted perspective camera.

This repository currently contains a **single-player vs AI prototype**: a human player and
one AI teammate face two AI opponents in a full match.

## Status: Prototype

- 3D court + net + perspective camera
- Ball physics (arc, bounce, net collision)
- Player movement, jump, serve/hit
- AI teammate + opponents (predict landing → chase → return)
- Rally rules: serve, max 3 touches per side, in/out detection, rally scoring
- Touch controls (on-screen joystick + jump/hit) and keyboard
- Procedurally-baked human characters with simple animation (idle / run / jump / swing),
  in per-player team colours — generated in code, no art files needed (real pixel art can
  drop in later by replacing the baked frames)
- Procedural sound (`GameAudio`), also synthesised in code with no audio files: a looping
  beach ambience, a continuous soft sand-shuffle that swells as players move, and one-shot
  SFX for ball contacts (by hit type), net hits, the serve whistle, a crowd cheer on points,
  match win, and the ball landing in vs out of bounds. Volumes are tunable in `GameConfig`

## Requirements

- **Unity 6000.4.8f1** (Unity 6 LTS) — see `ProjectSettings/ProjectVersion.txt`
- URP, Input System, and 3D physics (already in `Packages/manifest.json`)

## Getting started

1. Open the project in Unity Hub with Unity `6000.4.8f1`.
2. Run the menu command **`Volleyball → Build Prototype Scene`**. This generates
   `Assets/Scenes/Game.unity` with the court, net, camera, players, ball, and UI fully
   wired (placeholder art).
3. Open `Assets/Scenes/Game.unity` and press **Play**.

## Controls

| Action | Keyboard | Mouse | Touch |
| ------ | -------- | ----- | ----- |
| Move   | WASD / Arrow keys | — | On-screen joystick (left) |
| Jump   | Space | — | Jump button |
| Bump (over the net) | J | Left-click | Bump button |
| Set (up, your own side) | K | — | Set button |
| Spike (attack over the net) | L | Right-click | Spike button |
| Dive (desperate dig) | ; or Left Shift | — | — |

You choose each contact explicitly:

- **Bump** is a controlled pass. By default it stays on **your own side** (up toward the
  net for a teammate). Hold the stick/keys **toward the opponents' side** while you bump to
  send it over the net instead.
- **Set** keeps the ball on your side, lofted near the net to set up an attack.
- **Spike** drives it over the opponents' court — steep and fast when you hit it at the top
  of a jump (Jump, then Spike).
- **Dive** is a committed lunge along the direction you're holding (or the way you were last
  running) that covers ground much faster than running. You lay out flat on the sand as you
  slide; if a low ball comes into reach you dig it up — but chaotically: it pops high with a
  big random spray, an uncontrolled emergency touch rather than a pass. Then you're stuck on
  the ground until you stand back up, so a whiffed dive takes you out of the play.
- **Block** is automatic: **jump right next to the net** as an opponent attacks, and if the
  ball comes into your reach you'll stuff it straight back down onto their side. Timing your
  jump is the skill — no button needed. **You may not block a serve.**

**Serving** (when it's your serve, from behind the back line):
- **Underhand serve** — press **Bump (J)** to send it straight over.
- **Jump serve** — press **Set (K)** to toss the ball up, then **Jump (Space)** and
  **Spike (L)** at the top to hammer it over, flatter and faster. Miss the toss and it
  settles back so you can try again.

Because the same player can't touch the ball twice in a row, an offensive play is
*you set → AI partner spikes*, or *AI sets → you spike*. Aim by holding a movement
direction as you hit.

## Characters

Every player on court is one of a roster of characters, each with stats that trade off
against each other (defined in code in `Assets/Scripts/Player/CharacterDef.cs`):

- **Height** — scales the sprite and the vertical hit/block reach, tightens spike and
  block contacts, and directly scales how hard driven spikes and blocks come off the hand.
  Tall characters dominate the net; short ones give that up.
- **Speed** — scales run speed and the dive lunge.
- **Control** — tightens bumps, sets, serves and dive digs (divides their contact error),
  so touches land where they were aimed.

| Character | Height | Speed | Control | Identity |
| --------- | ------ | ----- | ------- | -------- |
| **Ace**   | 1.00   | 1.00  | 1.00    | balanced all-rounder (dark hair) |
| **Tower** | 1.16   | 0.85  | 0.90    | net dominance, slow (tall, black hair, deep skin) |
| **Bolt**  | 0.88   | 1.25  | 0.95    | court coverage, small at the net (short, blond) |
| **Sage**  | 0.95   | 0.90  | 1.35    | surgical ball control (auburn hair, pale skin) |
| **Rex**   | 1.22   | 0.80  | 0.85    | extreme skyscraper, clumsy (silver hair) |
| **Dot**   | 0.82   | 1.20  | 1.15    | tiny libero — quick and clean, no block (pink hair) |
| **Viper** | 1.05   | 1.15  | 0.80    | big fast wildcard, sloppy touches (green hair) |
| **Pearl** | 1.08   | 0.85  | 1.15    | tall steady setter, heavy feet (platinum hair, dark skin) |

**Quick Play opens a character-select screen**: pick from the roster, preview each
character's portrait, blurb and stat bars, then Play — you become that character and the
three AI players draw random (distinct) roster characters, so every match is a different
matchup. Your last pick is remembered. Campaign matches keep each scene's built-in cast.

Character sprites are baked per character and jersey colour — the height stat literally
makes the sprite taller (longer legs/torso, same head), and skin/hair colours identify who
is who while the jersey stays per-player-coloured. They are baked into
`Assets/Resources/Characters/` so the game can re-dress players at runtime when characters
are chosen after a scene was built. A `VolleyPlayer`'s character is chosen by its
`characterId` field. After tweaking a character's stats or colours, delete
`Assets/Resources/Characters/` to force a re-bake, and re-run the scene builders (the old
`Assets/Sprites/Characters/` folder from earlier builds is unused and can be deleted).

## Project layout

```
Assets/
  Scripts/{Ball,Player,AI,Court,Game,Camera,UI,Common}/   gameplay code
  Editor/                                                  scene-builder menu commands
  Sprites/{Characters,Ball,Court,UI}/                      art (placeholders for now)
  Prefabs/                                                 generated prefabs
  Scenes/Game.unity                                        the playable scene
  Settings/                                                URP render pipeline assets
```

## Level design — make any scene playable

The prototype scene above is generated all-at-once. For building *new* levels you instead
compose the reusable gameplay "keys" into a scene you've dressed however you like:

- **`Volleyball → Level Designer`** opens a small window. **Drop In Playable Court** injects
  the core keys (ground + net, ball, players, `MatchManager`, input and HUD) into the
  **currently open scene**. It's *additive and idempotent* — anything that already exists is
  skipped, so your set dressing is never clobbered and re-running is safe.
- **`Volleyball → Build Sunset Beach Arena Scene`** generates a showcase level
  (`Assets/Scenes/BeachArena.unity`): a procedural golden-hour beach — sunset skybox + sun,
  ocean, grandstands with a crowd, palms, tiki torches, umbrellas, clouds — with a fully
  playable court dropped in.

Gameplay is locked to a court centred on the **world origin** (player clamping, in/out
detection and camera-relative controls all read `CourtGeometry`'s origin-centred constants),
so build your decorations around `(0,0,0)`. All decorations are collider-stripped, so the
only surfaces the ball can hit remain the ground and the net.

The keys live in `Assets/Editor/CourtKit.cs`, the environment in `ArenaDecorator.cs`, and the
menus/window in `VolleyballLevelBuilder.cs` — independent of `PrototypeSceneBuilder.cs`.

## Building

- **WebGL:** File → Build Settings → WebGL. Serve the output over HTTP (it will not run
  from `file://`). Uses the PC URP renderer.
- **Mobile (Android/iOS):** switch platform, force **Landscape** orientation. Uses the
  Mobile URP renderer. On-screen touch controls appear automatically when a touchscreen is
  detected.

See `Assets/Editor/PrototypeSceneBuilder.cs` for how the scene is assembled.
