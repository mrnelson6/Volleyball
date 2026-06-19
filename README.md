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

You choose each contact explicitly:

- **Bump** is a controlled pass. By default it stays on **your own side** (up toward the
  net for a teammate). Hold the stick/keys **toward the opponents' side** while you bump to
  send it over the net instead.
- **Set** keeps the ball on your side, lofted near the net to set up an attack.
- **Spike** drives it over the opponents' court — steep and fast when you hit it at the top
  of a jump (Jump, then Spike).
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
