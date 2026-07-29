using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// Tags the net, the sibling of <see cref="GroundMarker"/>. It mattered less while the net was
    /// the only thing besides the floor the ball could hit; now that the set dressing is solid
    /// (see <c>DecorColliders</c>), the ball needs to tell a net brush from a thump off the
    /// grandstand. Added to the net by the scene builder.
    /// </summary>
    public class NetMarker : MonoBehaviour { }
}
