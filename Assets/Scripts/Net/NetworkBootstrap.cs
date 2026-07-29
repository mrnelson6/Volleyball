using Unity.Netcode;
using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// Creates the networking runtime on demand: instantiates the NetworkBootstrap prefab
    /// (NetworkManager + UnityTransport + session controller) and registers the dynamically
    /// spawned network prefabs (the lobby state). Called by every online entry point — the
    /// Online menu and the dev debug HUD — and by nothing else, which is the offline-dormancy
    /// guarantee: solo and campaign play never create a NetworkManager at all.
    /// </summary>
    public static class NetworkBootstrap
    {
        public const string BootstrapResource = "NetworkBootstrap";
        public const string LobbyStateResource = "LobbyState";

        public static NetworkManager Ensure()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null)
            {
                var prefab = Resources.Load<GameObject>(BootstrapResource);
                if (prefab == null)
                {
                    Debug.LogError("[Volleyball] NetworkBootstrap prefab missing — run " +
                                   "'Volleyball → Build World Tour (Everything)' in the editor.");
                    return null;
                }
                Object.Instantiate(prefab);
                nm = NetworkManager.Singleton;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            // Browsers cannot speak UDP: Relay traffic must ride secure WebSockets. Native
            // platforms keep DTLS — Relay bridges both connection types in one session, so
            // browser and desktop players share a match.
            var webTransport = nm.NetworkConfig.NetworkTransport
                as Unity.Netcode.Transports.UTP.UnityTransport;
            if (webTransport != null) webTransport.UseWebSockets = true;
#endif

            // Dynamically-spawned prefabs (LobbyState) are NOT registered here: NGO's
            // auto-generated DefaultNetworkPrefabs list (Assets/DefaultNetworkPrefabs.asset,
            // referenced by the bootstrap's NetworkManager) already contains every
            // NetworkObject prefab in the project — a runtime AddNetworkPrefab on top of it
            // is a guaranteed "duplicate GlobalObjectIdHash" error. That asset must stay
            // committed alongside the prefabs it registers.
            return nm;
        }

        /// <summary>Spawn the lobby state object (server only, once per session).</summary>
        public static OnlineLobbyState SpawnLobbyState()
        {
            if (OnlineLobbyState.Instance != null) return OnlineLobbyState.Instance;
            var prefab = Resources.Load<GameObject>(LobbyStateResource);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab);
            go.GetComponent<NetworkObject>().Spawn(destroyWithScene: false);
            return go.GetComponent<OnlineLobbyState>();
        }
    }
}
