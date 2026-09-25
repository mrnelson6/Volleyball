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
    /// Optional <c>-vbshotcount N</c> and <c>-vbshotarena &lt;SceneName&gt;</c> (default BeachArena).
    /// <c>-vbshotmenu</c> instead captures the character-select screen with a few animals picked;
    /// <c>-vbshotknock</c> bowls a player over mid-match and captures the knockdown sequence.
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
            runner.arena = Arg("-vbshotarena") ?? SceneFlow.BeachArena;
            runner.menu = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-vbshotmenu") >= 0;
            runner.knock = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-vbshotknock") >= 0;
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
        public string arena = SceneFlow.BeachArena;
        public bool menu;
        public bool knock;

        IEnumerator Start()
        {
            Directory.CreateDirectory(outputDir);
            Debug.Log($"[Volleyball] SCREENSHOT TOUR -> {outputDir} ({count} shots)");
            if (menu)
            {
                yield return MenuTour();
                Application.Quit(0);
                yield break;
            }

            // all four slots AI, so rallies play out with nobody at the keyboard
            var cfg = MatchConfig.Solo("fox", "bear", "penguin", "giraffe");
            for (int i = 0; i < cfg.slots.Length; i++) cfg.slots[i].occupant = SlotOccupant.AI;
            MatchSetup.Current = cfg;
            SceneManager.LoadScene(arena);
            yield return null; // scene objects Awake/Start
            NetSlotBinder.BindAll(FindAnyObjectByType<MatchManager>(), cfg);
            yield return new WaitForSeconds(2.5f); // serve toss, first rally moving

            if (knock)
            {
                // bowl the nearest-to-camera player over and film it (authority: offline)
                VolleyPlayer victim = null;
                foreach (var p in FindObjectsByType<VolleyPlayer>(FindObjectsSortMode.None))
                    if (victim == null || p.SimPosition.x > victim.SimPosition.x) victim = p;
                victim?.KnockDown(new Vector3(-1f, 0f, 0.3f));
            }

            for (int i = 0; i < count; i++)
            {
                string path = Path.Combine(outputDir, $"shot_{i:00}.png");
                ScreenCapture.CaptureScreenshot(path);
                yield return new WaitForSeconds(knock ? 0.12f : ScreenshotTour.Interval);
            }
            yield return new WaitForSeconds(0.5f); // last capture flushes at end of frame
            Debug.Log("[Volleyball] SCREENSHOT TOUR done");
            Application.Quit(0);
        }

        IEnumerator MenuTour()
        {
            SceneManager.LoadScene(SceneFlow.MainMenu);
            yield return new WaitForSeconds(1.5f);
            ScreenCapture.CaptureScreenshot(Path.Combine(outputDir, "menu_home.png"));
            yield return new WaitForSeconds(0.5f);

            CharacterSelectPanel panel = null;
            foreach (var p in FindObjectsByType<CharacterSelectPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                panel = p;
            if (panel == null) { Debug.LogError("[Volleyball] SCREENSHOT TOUR: no CharacterSelectPanel"); yield break; }
            panel.gameObject.SetActive(true);

            string[] picks = { "fox", "lion", "giraffe", "penguin", "moose", "toucan" };
            foreach (var id in picks)
            {
                panel.Select(id);
                yield return new WaitForSeconds(0.6f); // mid-cheer
                ScreenCapture.CaptureScreenshot(Path.Combine(outputDir, $"select_{id}.png"));
                yield return new WaitForSeconds(0.4f);
            }
            panel.gameObject.SetActive(false);

            // the other full-screen panels, one at a time (layout / cropping checks)
            foreach (var type in new[] { typeof(SettingsPanel), typeof(CampaignPanel), typeof(OnlinePanel) })
            {
                MonoBehaviour other = null;
                foreach (var p in FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.None))
                    other = (MonoBehaviour)p;
                if (other == null) continue;
                other.gameObject.SetActive(true);
                yield return new WaitForSeconds(0.6f);
                ScreenCapture.CaptureScreenshot(Path.Combine(outputDir, $"panel_{type.Name}.png"));
                yield return new WaitForSeconds(0.3f);
                other.gameObject.SetActive(false);
            }
            yield return new WaitForSeconds(0.5f);
            Debug.Log("[Volleyball] SCREENSHOT TOUR (menu) done");
        }
    }
}
