using Unity.Netcode;

namespace Volleyball
{
    /// <summary>
    /// The one place dual-mode questions get answered. Offline means literally no
    /// NetworkManager is listening — every adapter checks here and goes dormant, so the
    /// solo/campaign code path never executes a single Netcode call.
    /// </summary>
    public static class NetworkSession
    {
        /// <summary>True while a networked session is running (host, server, or client).</summary>
        public static bool IsOnline
            => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        /// <summary>True when THIS machine is the authority. Offline counts as authority —
        /// the solo game is just a server with one local player.</summary>
        public static bool IsAuthority => !IsOnline || NetworkManager.Singleton.IsServer;

        /// <summary>True only on a connected client that is NOT the server — the machines
        /// that mirror match state instead of computing it.</summary>
        public static bool IsRemoteClient => IsOnline && !NetworkManager.Singleton.IsServer;
    }
}
