# CLAUDE.md — Animal Volleyball

Unity 6 (6000.4.8f1) beach-volleyball game, single `Assembly-CSharp`, namespace
`Volleyball`. Fully server-authoritative online multiplayer (Netcode for
GameObjects + UGS Sessions/Relay) with client prediction. Gameplay docs live in
`README.md`; this file is operational knowledge: invariants, building, and the
production stack.

## Hard invariants — violating these has bitten us before

- **Scenes are generated, never hand-edited.** Every arena + the menu is built
  by editor scripts (`Assets/Editor/CourtKit.cs` is the core assembler). Any
  builder change ⇒ regenerate (`Volleyball → Build World Tour (Everything)`)
  **and commit the scenes** — in-scene `GlobalObjectIdHash` values must match
  across every build or clients explode on join. The hash-refresh pass at the
  end of the build exists because builders bake hash 0 into unsaved scenes.
- **Simulation vs view.** Players simulate at a fixed 50Hz tick:
  `VolleyPlayer.Simulate(InputCommand, dt, SimRole)` must stay a pure function
  of (state, command, dt) — no `Camera.main`, no `Time.time`, no randomness, no
  audio/UI (contact-error rolls are authority-side in `Execute*Authoritative`).
  Gameplay reads `SimPosition`; anything differentiating position per rendered
  frame (run cycles, movement audio) reads `ViewGroundPosition`/the transform,
  and interpolation clocks use `Time.time`, never `Time.fixedTime` — the
  50Hz-stepped values strobe animations (the twice-bitten "twitchy legs" bug).
- **The command stream IS the request channel.** Hits, serves, power-ups ride
  in `InputCommand`s that the server simulates; there are no separate request
  RPCs. Netcode lives in adapter components (`Assets/Scripts/Net/`) beside the
  plain-MonoBehaviour game classes — `VolleyPlayer`/`MatchManager`/
  `BallController` are never NetworkBehaviours (keeps offline dormant and
  runtime controller swaps legal).
- **Never `AddNetworkPrefab` at runtime.** `Assets/DefaultNetworkPrefabs.asset`
  (auto-generated, committed) already registers every prefab; adding again =
  duplicate-hash error.
- **Offline dormancy:** no `NetworkManager` exists outside the online flow.
  Only `NetworkBootstrap.Ensure()` creates one.

## Building (all headless-capable; Unity editor must be CLOSED for batch runs)

Editor at `C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe`.
Check no editor is running first (`Temp/UnityLockfile` absent).

- Full regen (sprites, prefabs, all 15 arenas, menu, hash refresh):
  `-batchmode -nographics -quit -projectPath <proj> -executeMethod Volleyball.EditorTools.WorldTourBuildAll.Build`
- Player builds: `...BuildKit.BuildWindows` / `.BuildWebGL` / `.BuildLinuxServer`
  → `Builds/`. Env var `VB_VERSION` stamps `PlayerSettings.bundleVersion`; the
  connect-time version handshake only lets identical stamps play together.
- Tests: `-runTests -testPlatform EditMode` (determinism suite in
  `Assets/Editor/Tests/`).
- Compile check without Unity (works with editor open): Unity's Roslyn —
  see the `unity-headless-build` memory; sources globbed from `Assets/`,
  references from the generated csproj HintPaths.
- Server self-tests on any player build: `-vbserver` (offline all-AI smoke,
  exit 0/1), `-vbhost` (create real Relay session, print join code, dedicated
  lobby, idle-exit after 10 empty minutes).

## Releasing to production

```
git commit → close editor → powershell -File Tools\deploy.ps1
```
Builds Linux server + Windows + WebGL stamped `<version>+<git-hash>`, zips the
Windows client (`Builds/Volleyball-win-*.zip` — send to friends; old builds get
a polite version-mismatch rejection), pushes server + WebGL to the box over
SSH. `-SkipWebGL` for fast server-only iterations. Config in gitignored
`Tools/deploy.config.json`. Commit BEFORE deploying — the stamp embeds HEAD's
hash. Browser players just refresh; marvin needs nothing (each spawned match
uses the binary on disk at spawn time).

## Production stack (live at https://volleyball.ttnelson.com)

```
DNS A: volleyball.ttnelson.com → 76.88.83.231
  → Pi "pi-proxy" (nelly@192.168.0.196) — Caddy, TLS termination
      /etc/caddy/Caddyfile: site block imports (secheaders), redacted access
      log. NB: new log files need `sudo touch` + `chown caddy:` BEFORE reload
      (systemd sandboxing), or the reload fails.
  → marvin (marvin@192.168.0.240) — game box
      Caddy :8090 (LAN-only): serves /var/www/volleyball (WebGL; .br files need
        the Content-Encoding/Content-Type headers already in its Caddyfile)
        and proxies /spawn + /status → 127.0.0.1:8765
      vb-spawn.py (systemd user unit volleyball-spawn): POST /spawn launches
        ~/volleyball/server/Volleyball.x86_64 -vbhost → returns {"code": ...};
        max 4 concurrent; logs in ~/volleyball/logs/
```

- Game traffic never touches this stack — clients (browser included) connect
  outbound to Unity Relay; the in-game "Server Match" button POSTs /spawn and
  auto-joins the returned code.
- SSH from this PC to both boxes works via keys (BatchMode-safe); marvin has
  passwordless-sudo NOT confirmed, the Pi's `nelly` does have it.
- **marvin may be asleep** (watch.ttnelson.com has a wake page for Jellyfin) —
  failed spawns / unreachable :8090 usually mean that, not a bug.
- UGS project is linked (anonymous auth); Relay/Lobby free tier. WebGL needs
  HTTPS (Brotli + wss) — it will not load over plain LAN http.

## Testing multiplayer locally

Multiplayer Play Mode (Window → Multiplayer Play Mode) for 2–4 virtual
players; dev IMGUI overlay in the menu (editor/dev builds) hosts/joins on
localhost without UGS. Network Simulator (Multiplayer Tools) for latency.
Prediction health: "last correction" in the overlay should sit ≈ 0.
