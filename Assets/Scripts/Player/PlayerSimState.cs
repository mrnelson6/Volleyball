using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// The complete simulated state of one player, stepped by
    /// <see cref="VolleyPlayer.Simulate"/> at the fixed tick. Everything gameplay needs to
    /// resume a player mid-flight lives here — so a snapshot can be captured, sent over the
    /// network, and re-applied for client-side prediction and reconciliation. The transform
    /// is deliberately NOT part of this: it is only the rendered, interpolated view.
    /// </summary>
    [System.Serializable]
    public struct PlayerSimState : Unity.Netcode.INetworkSerializable
    {
        /// <summary>World position; y is the jump height above the ground.</summary>
        public Vector3 position;
        public float vertVel;

        public float hitCooldown;
        public float bufferTime;      // remaining life of the buffered hit press
        public HitType bufferedHit;
        public bool swingWantedPrev;  // for the swing-on-press edge

        public float diveTimer;       // > 0 while sliding along the dive
        public float diveRecover;     // > 0 while getting back up afterwards
        public Vector3 diveDir;
        public Vector2 lastMoveDir;   // last non-zero steer, so a stationary dive has a direction

        /// <summary>Rides in every server snapshot (and back through reconciliation).</summary>
        public void NetworkSerialize<T>(Unity.Netcode.BufferSerializer<T> serializer)
            where T : Unity.Netcode.IReaderWriter
        {
            serializer.SerializeValue(ref position);
            serializer.SerializeValue(ref vertVel);
            serializer.SerializeValue(ref hitCooldown);
            serializer.SerializeValue(ref bufferTime);
            serializer.SerializeValue(ref bufferedHit);
            serializer.SerializeValue(ref swingWantedPrev);
            serializer.SerializeValue(ref diveTimer);
            serializer.SerializeValue(ref diveRecover);
            serializer.SerializeValue(ref diveDir);
            serializer.SerializeValue(ref lastMoveDir);
        }
    }
}
