# Animal Volleyball World Tour — 2.5D 2v2 Pixel-Art Volleyball

A 2.5D pixel-art **2v2 volleyball** game built in Unity 6, targeting **web browsers
(WebGL)** and **mobile** (touch), starring a cast of **35 animals** whose real-world traits
are their stats — the giraffe is tall, the cougar is fast, the buffalo is strong. Players
move on a 3D court (X/Z ground plane) while the ball arcs through 3D space (Y height) using
real physics. Characters and the ball are pixel-art sprites that billboard toward a tilted
perspective camera.

The campaign is a **world tour**: you and your partner (Finn the Fox + Bruno the Bear)
travel the globe, and at each stop you must win a **regional mini-tournament** against
duos of animals native to that region, on a court with that region's own **environmental
quirks** — thin Himalayan air, outback crosswinds, humid Amazon drag — before travelling on.

## Status

- 3D court + net + perspective camera; ball physics (arc, bounce, net collision)
- Player movement, jump, serve/hit; AI teammate + opponents (predict landing → chase → return)
- Rally rules: serve, max 3 touches per side, in/out detection, rally scoring
- Touch controls (on-screen joystick + jump/hit) and keyboard
- **World-tour campaign**: 9 stops (8 regional courts + the Cloud Kingdom World Finals),
  33 matches, per-match AI difficulty ramp, saved progress (JSON, versioned)
- **Regional environments**: per-region gravity / wind (with gusts) / ball drag / ambience,
  applied at match start by `CourtEnvironment` — the AI compensates for constant wind but
  not gusts, so weather reads as honest misjudgement
- Procedurally-baked animal characters (shared bipedal rig + per-species head/ears/horns/
  neck/tail/markings parameters), in per-player team colours — generated in code, no art files
- Procedural sound (`GameAudio`), also synthesised in code with no audio files: regional
  ambience beds (surf / wind / jungle / rain / snow), a movement shuffle, and one-shot SFX
  for contacts, net, whistle, points, match win and landings. Volumes tunable in `GameConfig`

## Requirements

- **Unity 6000.4.8f1** (Unity 6 LTS) — see `ProjectSettings/ProjectVersion.txt`
- URP, Input System, and 3D physics (already in `Packages/manifest.json`)

## Getting started

1. Open the project in Unity Hub with Unity `6000.4.8f1`.
2. Run the menu command **`Volleyball → Build World Tour (Everything)`**. This bakes every
   animal's sprites and generates all 15 arena scenes plus the main menu, fully wired.
3. Open `Assets/Scenes/MainMenu.unity` and press **Play**.

## Controls

| Action | Keyboard | Mouse | Touch |
| ------ | -------- | ----- | ----- |
| Move   | WASD / Arrow keys | — | On-screen joystick (left) |
| Jump   | Space | — | Jump button |
| Bump (over the net) | J | Left-click | Bump button |
| Set (up, your own side) | K | — | Set button |
| Spike (attack over the net) | L | Right-click | Spike button |
| Dive (desperate dig) | ; or Left Shift | — | Dive button |
| "I got it!" | Z | Click the button | I GOT IT button (bottom) |
| "You got it!" | X | Click the button | YOU GOT IT button (bottom) |
| Emotes | 1–6 | Click the button | `:)` button (bottom) |
| Pause / controls page | Esc | Click MENU | MENU button |

Pausing (**Esc** or **MENU**) shows this control list on screen, so you never have to leave
the match to check a binding.

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

### Talking to your partner

Two calls, and your partner actually listens to them — an AI teammate or a human one:

- **"I got it!" (Z)** — you're taking this ball. AI teammates stop pursuing it and drop
  into cover instead of crowding you.
- **"You got it!" (X)** — it's theirs. The teammate nearest the ball goes for it even when
  they'd normally have deferred to whoever was closest.

A call lasts about a second and a half, and is spent the moment your team touches the ball,
so it can never leave a teammate standing around. The voice is yours alone: an AI partner
only ever *listens*, and with nothing said it plays exactly as it always did. The remaining
**emotes (1–6)** are pure expression: a bubble and a blip, no effect on play.

Callouts travel in the same per-tick command stream as hits and serves, so online the server
decides who said what and every screen sees the same bubble.

## Characters — the animal roster

Every player on court is one of **35 animals**, each with stats that mirror the real
creature and trade off against each other (defined in code in
`Assets/Scripts/Player/CharacterDef.cs`):

- **Height** — scales the sprite and the vertical hit/block reach and tightens spike and
  block contacts. Tall animals (giraffe, moose, camel) dominate the net.
- **Speed** — scales run speed and the dive lunge (cougar, hare, jaguar).
- **Power** — raw strength: scales the pace of driven spikes and blocks (buffalo, yak,
  polar bear), independent of height.
- **Control** — tightens bumps, sets, serves and dive digs (penguin, raccoon, capybara).
- **Jump** — jump height, applied as sqrt so apex height scales linearly (kangaroo,
  jerboa, markhor).

The protagonists are **Finn the Fox** (you — quick and clever) and **Bruno the Bear**
(your teammate — big paws, bigger spikes). The other 33 animals are grouped by home
region — savanna (meerkat, zebra, warthog, giraffe, lion), Amazon (capybara, toucan,
sloth, jaguar), outback (wombat, dingo, emu, kangaroo), Himalaya (red panda, yak, markhor,
snow leopard), Black Forest (hare, badger, boar, stag), Sahara (jerboa, fennec, oryx,
camel), Rockies (raccoon, moose, buffalo, cougar) and Arctic (penguin, snowy owl, walrus,
polar bear). Full stat lines live in the roster table in `CharacterDef.cs`.

**Quick Play opens a character-select screen**: pick any animal from the scrollable
roster, preview its portrait, blurb and five stat bars, choose a **venue** (any of the 15
courts — regional venues keep their weather), then Play — the two opponents draw random
roster animals while your teammate stays Bruno. Your last pick and venue are remembered.

Sprites are baked per animal and jersey colour from **one shared bipedal rig** plus
per-species `SpeciesArt` parameters (head shape, ears, horns/antlers/tusks, neck length,
tail, stripe/spot/mask markings) into `Assets/Resources/Characters/`, so the game can
re-dress players at runtime. The cache is stamped with an art version and wipes itself
when the draw code changes; use **`Volleyball → Force Rebake Character Sprites`** after
tweaking a single animal's colours, and **`Volleyball → Save Character Contact Sheet`**
to render the whole cast into one PNG for a quick look.

## The World Tour campaign

Campaign progress lives in `Assets/Scripts/Campaign/RegionDef.cs` (`RegionRoster` — the
tour ladder) and a versioned JSON save (`SaveSystem` / `CampaignSave`). Each region defines
its court scene, its species pool, a tournament of 3–4 named opponent duos with a per-match
AI difficulty (`aiErrorMult`, reaction scale), and an `EnvironmentProfile`:

| # | Region | Court quirk |
| - | ------ | ----------- |
| 1 | Sunny Savanna | baseline physics (the tutorial stop) |
| 2 | Amazon Rainforest | humid air — ball drag 0.2 |
| 3 | Australian Outback | crosswind with gusts |
| 4 | Himalayan Peaks | thin air — 0.85× gravity, floaty everything |
| 5 | Black Forest | drizzle — ball drag 0.15 |
| 6 | Sahara Dunes | strong gusting sandstorm wind |
| 7 | Rocky Mountains | wind straight down the court |
| 8 | Polar Ice | 0.95× gravity, icy hush |
| 9 | Cloud Kingdom Finals | 0.9× gravity showcase (reuses the SkyArena) |

Win a match to advance the bracket; lose and you retry it. Clearing a region unlocks the
next stop on the tour board (Campaign menu), and results save immediately —
quitting at the end-of-match banner loses nothing. `MatchManager` routes the Hit key at
match end to the next match, a retry, or back to the tour board.

## Project layout

```
Assets/
  Scripts/{Ball,Player,AI,Court,Game,Camera,UI,Common,Campaign}/   gameplay code
  Editor/                                                  scene-builder menu commands
  Sprites/{Characters,Ball,Court,UI}/                      art (placeholders for now)
  Prefabs/                                                 generated prefabs
  Scenes/                                                  MainMenu + 15 arena scenes
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
- **`Volleyball → Themed Levels`** / **`Build All Themed Arenas`** builds the other 14
  courts from data: 6 fantasy venues (`ThemedArenaDecorator`) and the 8 world-tour regional
  courts (`RegionalArenaThemes`, keyed to `RegionRoster` scene names). Cosmetics live in
  the theme; each region's *gameplay* weather lives in its `EnvironmentProfile` and is
  applied at runtime by `CourtEnvironment` — including in Quick Play.

Gameplay is locked to a court centred on the **world origin** (player clamping, in/out
detection and camera-relative controls all read `CourtGeometry`'s origin-centred constants),
so build your decorations around `(0,0,0)`. Decorations are **solid** — a ball hit into the
stands thumps off the concrete instead of passing through it — which `DecorColliders` applies
as a post-pass at the end of every arena build. It deliberately leaves three things
pass-through: anything overlapping the play volume (the court plus a margin, net posts
included), the horizon-wide ground sheets that sit just under the sand, and props floating
out of the ball's reach. Scoring still comes from the `GroundMarker` plane alone, and a ball
that ends up somewhere it can never land from is resolved by the rally watchdog in
`MatchManager` (`rallyStallSeconds`).

The keys live in `Assets/Editor/CourtKit.cs`, the environment in `ArenaDecorator.cs`, and the
menus/window in `VolleyballLevelBuilder.cs` — independent of `PrototypeSceneBuilder.cs`.

## Building

- **WebGL:** File → Build Settings → WebGL. Serve the output over HTTP (it will not run
  from `file://`). Uses the PC URP renderer.
- **Mobile (Android/iOS):** switch platform, force **Landscape** orientation. Uses the
  Mobile URP renderer. On-screen touch controls appear automatically when a touchscreen is
  detected.

See `Assets/Editor/PrototypeSceneBuilder.cs` for how the scene is assembled.
