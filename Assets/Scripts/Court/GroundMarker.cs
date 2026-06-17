using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// Tags the floor so the ball can recognise a ground contact without relying on
    /// a project-specific Unity tag. Added to the ground plane by the scene builder.
    /// </summary>
    public class GroundMarker : MonoBehaviour { }
}
