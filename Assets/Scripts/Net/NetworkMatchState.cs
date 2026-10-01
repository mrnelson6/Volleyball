using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// The match's network mirror, next to <see cref="MatchManager"/>. On the server it
    /// publishes the match config (once) and a compact state snapshot (on change), routes
    /// slot claims as clients connect, and starts the match when every human slot is filled.
    /// On clients it writes the mirrored state back through
    /// <see cref="MatchManager.MirrorNetworkState"/>, so every existing reader — HUD,
    /// CanTeamTouch gates, glow — keeps working against the same API, and plays the
    /// transition audio (whistle, point, match win) its local viewer should hear.
    /// </summary>
    public class NetworkMatchState : NetworkBehaviour
    {
        MatchManager _match;
        bool _started;
        string _waitingText;

        // Clients that have finished loading/synchronizing this scene. The match must not
        // kick off until every ASSIGNED remote human is actually standing in the arena —
        // with a lobby, slots are claimed before the scene load, so connection alone is
        // not enough.
        readonly System.Collections.Generic.HashSet<ulong> _syncedClients =
            new System.Collections.Generic.HashSet<ulong>();

        struct MatchSnap : INetworkSerializable, IEquatable<MatchSnap>
        {
            public int scoreA, scoreB;
            public byte state, servingTeam, possession;
            public int touches;
            public bool serveInFlight, serveTossed;
            public byte bannerKind, bannerTeam;
            public FixedString128Bytes bannerText;
            public NetworkObjectReference server;
            public double introStart; // server network time the intro began (MatchIntro)

            public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
            {
                s.SerializeValue(ref scoreA);
                s.SerializeValue(ref scoreB);
                s.SerializeValue(ref state);
                s.SerializeValue(ref servingTeam);
                s.SerializeValue(ref possession);
                s.SerializeValue(ref touches);
                s.SerializeValue(ref serveInFlight);
                s.SerializeValue(ref serveTossed);
                s.SerializeValue(ref bannerKind);
                s.SerializeValue(ref bannerTeam);
                s.SerializeValue(ref bannerText);
                s.SerializeValue(ref server);
                s.SerializeValue(ref introStart);
            }

            public bool Equals(MatchSnap o)
                => scoreA == o.scoreA && scoreB == o.scoreB && state == o.state
                   && servingTeam == o.servingTeam && possession == o.possession
                   && touches == o.touches && serveInFlight == o.serveInFlight
                   && serveTossed == o.serveTossed && bannerKind == o.bannerKind
                   && bannerTeam == o.bannerTeam && bannerText.Equals(o.bannerText)
                   && server.NetworkObjectId == o.server.NetworkObjectId
                   && introStart == o.introStart;
        }

        readonly NetworkVariable<MatchSnap> _snap = new NetworkVariable<MatchSnap>();
        readonly NetworkVariable<FixedString4096Bytes> _configJson =
            new NetworkVariable<FixedString4096Bytes>();

        public override void OnNetworkSpawn()
        {
            _match = GetComponent<MatchManager>();
            if (IsServer) SpawnServer();
            else SpawnClient();
        }

        public override void OnNetworkDespawn()
        {
            AbilityDirector.Fired -= RelayAbilityFired;
            AbilityDirector.Moment -= RelayAbilityMoment;
            if (!IsServer || NetworkManager == null) return;
            ChatDirector.Relay = null;
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnectedServer;
            if (NetworkManager.SceneManager != null)
            {
                NetworkManager.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
                NetworkManager.SceneManager.OnSynchronizeComplete -= OnClientSynchronized;
            }
            if (_match != null)
            {
                _match.PositionsReset -= OnPositionsResetServer;
                _match.RallyEnded -= OnRallyEndedServer;
            }
        }

        // ------------------------------------------------------------------ server

        void SpawnServer()
        {
            MatchConfig cfg = MatchSetup.Current;
            if (cfg?.slots != null)
            {
                for (int i = 0; i < cfg.slots.Length; i++)
                    if (cfg.slots[i].occupant == SlotOccupant.LocalHuman)
                        cfg.slots[i].clientId = NetworkManager.ServerClientId;
                PublishConfig(cfg);
                NetSlotBinder.BindAll(_match, cfg);
                ApplyOwnership(cfg);
            }

            NetworkManager.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnectedServer;
            NetworkManager.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
            NetworkManager.SceneManager.OnSynchronizeComplete += OnClientSynchronized;
            _match.PositionsReset += OnPositionsResetServer;
            _match.RallyEnded += OnRallyEndedServer;
            ChatDirector.Relay = RelayChat; // callouts the server accepted go out to everyone
            TryStart();
        }

        bool _pendingAiTakeover;

        /// <summary>
        /// A client dropped mid-session: their slot becomes an AI's. The player freezes on
        /// the spot immediately (command stream silenced) and the actual controller swap
        /// happens at the next serve boundary — never mid-rally — unless no rally is running,
        /// in which case it's safe right away. Ownership has already reverted to the server
        /// (players are DontDestroyWithOwner).
        /// </summary>
        void OnClientDisconnectedServer(ulong clientId)
        {
            _syncedClients.Remove(clientId);
            MatchConfig cfg = MatchSetup.Current;
            if (cfg?.slots == null) return;

            for (int i = 0; i < cfg.slots.Length; i++)
            {
                if (cfg.slots[i].occupant != SlotOccupant.RemoteHuman
                    || cfg.slots[i].clientId != clientId) continue;

                cfg.slots[i].occupant = SlotOccupant.AI;
                cfg.slots[i].clientId = 0;
                PublishConfig(cfg);

                foreach (var p in FindObjectsByType<VolleyPlayer>(FindObjectsSortMode.None))
                    if (p.team == cfg.slots[i].team
                        && (p.halfSign < 0f) == (cfg.slots[i].halfSign < 0f))
                    {
                        p.GetComponent<NetworkPlayer>()?.OnOwnerDropped();
                        string who = CharacterRoster.Get(cfg.slots[i].characterId).displayName;
                        _match.ShowPowerBanner($"{who} dropped — AI takes over");
                        VBLog.Event($"NET client {clientId} dropped; slot {i} -> AI");
                        break;
                    }

                _pendingAiTakeover = true;
                if (_started && _match.State != MatchState.Rallying) ApplyPendingTakeovers();
                break;
            }
        }

        void ApplyPendingTakeovers()
        {
            if (!_pendingAiTakeover) return;
            _pendingAiTakeover = false;
            NetSlotBinder.BindAll(_match, MatchSetup.Current); // swaps only mismatched slots
        }

        void OnLoadEventCompleted(string sceneName, UnityEngine.SceneManagement.LoadSceneMode mode,
                                  System.Collections.Generic.List<ulong> completed,
                                  System.Collections.Generic.List<ulong> timedOut)
        {
            foreach (ulong id in completed) _syncedClients.Add(id);
            TryStart();
        }

        void OnClientSynchronized(ulong clientId)
        {
            _syncedClients.Add(clientId);
            TryStart();
        }

        void PublishConfig(MatchConfig cfg)
            => _configJson.Value = new FixedString4096Bytes(JsonUtility.ToJson(cfg));

        void ApplyOwnership(MatchConfig cfg)
        {
            var players = FindObjectsByType<VolleyPlayer>(FindObjectsSortMode.None);
            foreach (var slot in cfg.slots)
            {
                if (slot.occupant == SlotOccupant.AI) continue;
                if (slot.clientId == MatchConfig.UnassignedClient) continue;
                foreach (var p in players)
                {
                    if (p == null || p.team != slot.team
                        || (p.halfSign < 0f) != (slot.halfSign < 0f)) continue;
                    var no = p.GetComponent<NetworkObject>();
                    if (no != null && no.OwnerClientId != slot.clientId)
                        no.ChangeOwnership(slot.clientId);
                    break;
                }
            }
        }

        void OnClientConnected(ulong clientId)
        {
            MatchConfig cfg = MatchSetup.Current;
            if (cfg?.slots == null) return;
            for (int i = 0; i < cfg.slots.Length; i++)
            {
                if (cfg.slots[i].occupant != SlotOccupant.RemoteHuman) continue;
                if (cfg.slots[i].clientId != MatchConfig.UnassignedClient) continue;
                cfg.slots[i].clientId = clientId;
                VBLog.Event($"NET client {clientId} takes slot {cfg.slots[i].team}/{cfg.slots[i].halfSign}");
                PublishConfig(cfg);
                ApplyOwnership(cfg);
                break;
            }
            TryStart();
        }

        void TryStart()
        {
            if (_started) return;
            MatchConfig cfg = MatchSetup.Current;
            if (cfg?.slots == null) return;

            int humans = 0, present = 0;
            foreach (var s in cfg.slots)
            {
                if (s.occupant == SlotOccupant.AI) continue;
                humans++;
                if (s.clientId == MatchConfig.UnassignedClient) continue;
                // assigned — but a remote human must also have finished loading the arena
                if (s.clientId == NetworkManager.ServerClientId || _syncedClients.Contains(s.clientId))
                    present++;
            }

            if (present < humans)
            {
                string text = $"Waiting for players… ({present}/{humans})";
                if (text != _waitingText)
                {
                    _waitingText = text;
                    _match.SetBannerServer(BannerMessage.Raw(text));
                }
                return;
            }

            _started = true;
            _match.SetBannerServer(BannerMessage.None);
            SubscribePowerEvents();
            _match.BeginMatchServer();
        }

        void SubscribePowerEvents()
        {
            // signature abilities: the server fired one / announced a moment of one — every
            // client rebuilds it from the same params (see AbilityDirector.MirrorFire)
            AbilityDirector.Fired -= RelayAbilityFired;
            AbilityDirector.Fired += RelayAbilityFired;
            AbilityDirector.Moment -= RelayAbilityMoment;
            AbilityDirector.Moment += RelayAbilityMoment;

            foreach (var p in FindObjectsByType<VolleyPlayer>(FindObjectsSortMode.None))
            {
                VolleyPlayer captured = p;
                p.Power.Activated += _ =>
                {
                    var no = captured.GetComponent<NetworkObject>();
                    if (no != null) PowerActivatedRpc(no);
                };
            }
        }

        void Update()
        {
            if (!IsSpawned) return;

            if (IsServer)
            {
                if (!_started) TryStart(); // keeps the waiting banner fresh
                var b = _match.Banner;
                var snap = new MatchSnap
                {
                    scoreA = _match.ScoreA,
                    scoreB = _match.ScoreB,
                    state = (byte)_match.State,
                    servingTeam = (byte)_match.ServingTeam,
                    possession = (byte)_match.Possession,
                    touches = _match.Touches,
                    serveInFlight = _match.ServeInFlight,
                    serveTossed = _match.ServeTossed,
                    bannerKind = (byte)b.kind,
                    bannerTeam = (byte)b.team,
                    bannerText = new FixedString128Bytes(Truncate(b.text, 120)),
                    server = ServerPlayerRef(),
                    introStart = _match.IntroStartTime,
                };
                if (!snap.Equals(_snap.Value)) _snap.Value = snap;
            }
            else
            {
                // proxies' power-up statuses have no simulation ticking them — expire here
                foreach (var p in FindObjectsByType<VolleyPlayer>(FindObjectsSortMode.None))
                    if (!p.enabled) p.Power.Tick(Time.deltaTime);
            }
        }

        static string Truncate(string s, int max)
            => string.IsNullOrEmpty(s) ? "" : s.Length <= max ? s : s.Substring(0, max);

        NetworkObjectReference ServerPlayerRef()
        {
            VolleyPlayer sv = _match.CurrentServer;
            var no = sv != null ? sv.GetComponent<NetworkObject>() : null;
            return no != null ? (NetworkObjectReference)no : default;
        }

        void OnPositionsResetServer()
        {
            // serve boundary: the safe moment to hand a dropped human's slot to the AI
            ApplyPendingTakeovers();
            var players = FindObjectsByType<VolleyPlayer>(FindObjectsSortMode.None);
            var refs = new NetworkObjectReference[players.Length];
            var positions = new Vector3[players.Length];
            for (int i = 0; i < players.Length; i++)
            {
                var no = players[i].GetComponent<NetworkObject>();
                refs[i] = no != null ? (NetworkObjectReference)no : default;
                positions[i] = players[i].GroundPosition;
            }
            PositionsResetRpc(refs, positions);
        }

        void OnRallyEndedServer(TeamSide scorer, string reason) => RallyEndedRpc(scorer);

        void RelayAbilityFired(VolleyPlayer p, AbilityId id, AbilityParams prm)
        {
            var no = p != null ? p.GetComponent<NetworkObject>() : null;
            if (no != null) AbilityFiredRpc(no, (byte)id, prm);
        }

        void RelayAbilityMoment(VolleyPlayer p, int code, Vector3 at, Vector3 dir)
        {
            var no = p != null ? p.GetComponent<NetworkObject>() : null;
            if (no != null) AbilityMomentRpc(no, code, at, dir);
        }


        /// <summary>
        /// A callout the server accepted (from a human's command stream or an AI): tell every
        /// client who said what, so the bubble and its sound appear on every screen. The
        /// gameplay meaning stays server-side — clients only ever present it.
        /// </summary>
        void RelayChat(VolleyPlayer speaker, ChatCall call)
        {
            var no = speaker != null ? speaker.GetComponent<NetworkObject>() : null;
            if (no == null) return;
            ChatSaidRpc(no, (byte)call);
        }

        // ------------------------------------------------------------------ client

        void SpawnClient()
        {
            _configJson.OnValueChanged += (_, v) => ApplyConfigClient(v);
            if (_configJson.Value.Length > 0) ApplyConfigClient(_configJson.Value);
            _snap.OnValueChanged += OnSnapChanged;
            ApplySnap(_snap.Value);
        }

        void ApplyConfigClient(FixedString4096Bytes json)
        {
            MatchConfig cfg = JsonUtility.FromJson<MatchConfig>(json.ToString());
            if (cfg?.slots == null) return;
            MatchSetup.Current = cfg;
            NetSlotBinder.BindAll(_match, cfg);
            _match.ReapplyMatchSetup();
            foreach (var np in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
                np.Reconfigure();
        }

        void OnSnapChanged(MatchSnap prev, MatchSnap cur)
        {
            ApplySnap(cur);

            // audio the local viewer should hear, keyed off state TRANSITIONS — including
            // Intro -> Serving, the whistle that ends the intro cinematic
            if (prev.state != cur.state)
            {
                if ((MatchState)cur.state == MatchState.Serving) GameAudio.PlayWhistle();
                if ((MatchState)cur.state == MatchState.MatchOver) GameAudio.PlayMatchWin();
            }
            if (!prev.serveTossed && cur.serveTossed)
            {
                if (cur.server.TryGet(out NetworkObject no))
                    no.GetComponent<VolleyPlayer>()?.TriggerSwing(HitType.Set);
            }
        }

        void ApplySnap(MatchSnap s)
        {
            VolleyPlayer server = null;
            if (s.server.TryGet(out NetworkObject no)) server = no.GetComponent<VolleyPlayer>();
            var banner = new BannerMessage
            {
                kind = (BannerKind)s.bannerKind,
                team = (TeamSide)s.bannerTeam,
                text = s.bannerText.ToString(),
            };
            _match.MirrorNetworkState(s.scoreA, s.scoreB, (MatchState)s.state,
                                      (TeamSide)s.servingTeam, (TeamSide)s.possession, s.touches,
                                      s.serveInFlight, s.serveTossed, banner, server,
                                      s.introStart);
        }

        [Rpc(SendTo.NotServer)]
        void PositionsResetRpc(NetworkObjectReference[] refs, Vector3[] positions)
        {
            for (int i = 0; i < refs.Length; i++)
            {
                if (!refs[i].TryGet(out NetworkObject no)) continue;
                no.GetComponent<NetworkPlayer>()?.OnTeleported(positions[i]);
            }
        }

        [Rpc(SendTo.NotServer)]
        void RallyEndedRpc(TeamSide scorer)
        {
            TeamSide viewer = LocalViewerTeam();
            GameAudio.PlayPoint(scorer == viewer);
            foreach (var p in FindObjectsByType<VolleyPlayer>(FindObjectsSortMode.None))
                p.Power.MirrorRallyEnd();
            PowerUpDirector.RevertAll();
            AbilityDirector.EndAll();
        }

        [Rpc(SendTo.NotServer)]
        void AbilityFiredRpc(NetworkObjectReference playerRef, byte id, AbilityParams prm)
        {
            if (!playerRef.TryGet(out NetworkObject no)) return;
            var p = no.GetComponent<VolleyPlayer>();
            if (p == null) return;
            AbilityDirector.MirrorFire(p, (AbilityId)id, prm); // banner: match snapshot; sound: on start
        }

        [Rpc(SendTo.NotServer)]
        void AbilityMomentRpc(NetworkObjectReference playerRef, int code, Vector3 at, Vector3 dir)
        {
            if (!playerRef.TryGet(out NetworkObject no)) return;
            var p = no.GetComponent<VolleyPlayer>();
            if (p != null) AbilityDirector.MirrorMoment(p, code, at, dir);
        }

        [Rpc(SendTo.NotServer)]
        void ChatSaidRpc(NetworkObjectReference speakerRef, byte call)
        {
            if (!speakerRef.TryGet(out NetworkObject no)) return;
            var p = no.GetComponent<VolleyPlayer>();
            if (p != null) ChatDirector.Show(p, (ChatCall)call);
        }

        [Rpc(SendTo.NotServer)]
        void PowerActivatedRpc(NetworkObjectReference playerRef)
        {
            if (!playerRef.TryGet(out NetworkObject no)) return;
            var p = no.GetComponent<VolleyPlayer>();
            if (p == null) return;
            p.Power.MirrorActivate();
            GameAudio.PlayPowerUp(p.transform.position);
        }

        static TeamSide LocalViewerTeam()
        {
            foreach (var p in FindObjectsByType<VolleyPlayer>(FindObjectsSortMode.None))
                if (p.IsHuman && p.IsLocallyControlled) return p.team;
            return TeamSide.A;
        }
    }
}
