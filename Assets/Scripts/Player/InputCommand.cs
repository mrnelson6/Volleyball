using UnityEngine;

namespace Volleyball
{
    /// <summary>What the server wants to do with a held serve this tick.</summary>
    public enum ServeIntent : byte { None, Underhand, Toss, JumpStrike }

    /// <summary>
    /// Which role a <see cref="VolleyPlayer.Simulate"/> call plays. Offline everything is
    /// Authority. Online, the server simulates every player as Authority; the owning client
    /// steps its own player as Predict (movement + local feedback, but contacts, power-ups
    /// and serve actions stay server-side — they ride to the server in the command stream);
    /// Replay is Predict re-run during reconciliation — state only, no events, no sounds,
    /// and crucially no re-ticking of real-time effects.
    /// </summary>
    public enum SimRole : byte { Authority, Predict, Replay }

    /// <summary>
    /// How a hit command aims. A human aims by steering — the actual target point is derived
    /// inside the simulation at the moment of contact (so adjusting your aim during the hit
    /// buffer works, and the server can reproduce it from the same command). The AI plans an
    /// exact spot on the court, so its commands carry the point itself.
    /// </summary>
    public enum AimMode : byte { Steer, Explicit }

    /// <summary>
    /// One simulation tick's worth of player intent. Every controller — the local human, the
    /// AI, and (later) a remote human — produces exactly this, and <see cref="VolleyPlayer"/>
    /// consumes nothing else. Directions are world-space: the camera-relative conversion
    /// happens where the camera is (the local client), never inside the simulation.
    /// </summary>
    [System.Serializable]
    public struct InputCommand : Unity.Netcode.INetworkSerializable
    {
        /// <summary>Simulation tick this command belongs to.</summary>
        public int tick;

        /// <summary>World-space XZ steering, magnitude ≤ 1.</summary>
        public Vector2 moveWorld;

        public bool jump;        // pressed this tick
        public bool dive;        // pressed this tick
        public bool power;       // pressed this tick

        /// <summary>A hit press this tick, with the explicitly chosen contact.</summary>
        public bool hitPressed;
        public HitType hitType;

        /// <summary>Steer = derive the target from <see cref="moveWorld"/> at contact;
        /// Explicit = use <see cref="hitAim"/> as planned (AI).</summary>
        public AimMode aimMode;
        public Vector3 hitAim;

        /// <summary>Serve action this tick (only meaningful while this player holds the serve).</summary>
        public ServeIntent serve;

        /// <summary>A callout said this tick ("I got it", an emote, …). Like the hit and serve
        /// intents it is a REQUEST: only the authority acts on it (<see cref="ChatDirector"/>),
        /// so prediction and replay ignore it entirely.</summary>
        public ChatCall chat;

        public static InputCommand Empty(int tick) => new InputCommand { tick = tick };

        /// <summary>Commands travel client → server every tick; explicit field serialization
        /// keeps the wire format stable and bool/enum-safe.</summary>
        public void NetworkSerialize<T>(Unity.Netcode.BufferSerializer<T> serializer)
            where T : Unity.Netcode.IReaderWriter
        {
            serializer.SerializeValue(ref tick);
            serializer.SerializeValue(ref moveWorld);
            serializer.SerializeValue(ref jump);
            serializer.SerializeValue(ref dive);
            serializer.SerializeValue(ref power);
            serializer.SerializeValue(ref hitPressed);
            serializer.SerializeValue(ref hitType);
            serializer.SerializeValue(ref aimMode);
            serializer.SerializeValue(ref hitAim);
            serializer.SerializeValue(ref serve);
            serializer.SerializeValue(ref chat);
        }
    }
}
