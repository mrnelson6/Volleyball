using UnityEngine;
using UnityEngine.UI;

namespace Volleyball
{
    /// <summary>Shows the build's version stamp (e.g. "v0.1.0+c4df4cc") in a corner of the menu,
    /// so a player can tell at a glance whether they're on the current release.</summary>
    [RequireComponent(typeof(Text))]
    public class VersionLabel : MonoBehaviour
    {
        void Awake() => GetComponent<Text>().text = "v" + Application.version;
    }
}
