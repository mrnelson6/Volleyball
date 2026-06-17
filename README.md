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
- Placeholder/programmer art (real pixel art to be added later)

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

| Action | Keyboard | Touch |
| ------ | -------- | ----- |
| Move   | WASD / Arrow keys | On-screen joystick (left) |
| Jump   | Space | Jump button (right) |
| Hit / Serve | J / Left-click | Hit button (right) |

Bump vs spike is contextual: hitting while grounded bumps, hitting while airborne spikes.

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

## Building

- **WebGL:** File → Build Settings → WebGL. Serve the output over HTTP (it will not run
  from `file://`). Uses the PC URP renderer.
- **Mobile (Android/iOS):** switch platform, force **Landscape** orientation. Uses the
  Mobile URP renderer. On-screen touch controls appear automatically when a touchscreen is
  detected.

See `Assets/Editor/PrototypeSceneBuilder.cs` for how the scene is assembled.
