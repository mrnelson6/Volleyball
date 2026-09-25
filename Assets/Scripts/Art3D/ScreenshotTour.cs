using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Volleyball
{
    /// <summary>
    /// Visual smoke test for the art: launch a player build with <c>-vbshots &lt;dir&gt;</c> and it
    /// plays an offline all-AI Sunset Beach match (fox + bear vs penguin + giraffe), writes a screenshot
    /// every <see cref="Interval"/> seconds, then quits. The 3D overhaul's equivalent of
    /// <c>-vbserver</c>: proof in pictures that models, animation and lighting work in the real
    /// game loop, capturable headlessly (needs a GPU — don't pass -nographics).
    /// Optional <c>-vbshotcount N</c>.
    /// </summary>
    public static class ScreenshotTour
    {
        public const float Interval = 0.5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            string dir = Arg("-vbshots");
            if (dir == null || Object.FindAnyObjectByType<ScreenshotTourRunner>() != null) return;
            int.TryParse(Arg("-vbshotcount") ?? "12", out int count);

            var go = new GameObject("ScreenshotTour");
            Object.DontDestroyOnLoad(go);
            var runner = go.AddComponent<ScreenshotTourRunner>();
            runner.outputDir = dir;
            runner.count = Mathf.Max(1, count);
        }

        static string Arg(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }

    public class ScreenshotTourRunner : MonoBehaviour
    {
        public string outputDir;
        public int count = 12;

        IEnumerator Start()
        {
            Directory.CreateDirectory(outputDir);
            Debug.Log($"[Volleyball] SCREENSHOT TOUR -> {outputDir} ({count} shots)");

            // all four slots AI, so rallies play out with nobody at the keyboard
            var cfg = MatchConfig.Solo("fox", "bear", "penguin", "giraffe");
            for (int i = 0; i < cfg.slots.Length; i++) cfg.slots[i].occupant = SlotOccupant.AI;
            MatchSetup.Current = cfg;
            SceneManager.LoadScene(SceneFlow.BeachArena);
            yield return null; // scene objects Awake/Start
            NetSlotBinder.BindAll(FindAnyObjectByType<MatchManager>(), cfg);
            yield return new WaitForSeconds(2.5f); // serve toss, first rally moving

            for (int i = 0; i < count; i++)
            {
                string path = Path.Combine(outputDir, $"shot_{i:00}.png");
                ScreenCapture.CaptureScreenshot(path);
                yield return new WaitForSeconds(ScreenshotTour.Interval);
            }
            yield return new WaitForSeconds(0.5f); // last capture flushes at end of frame
            Debug.Log("[Volleyball] SCREENSHOT TOUR done");
            Application.Quit(0);
        }
    }
}
