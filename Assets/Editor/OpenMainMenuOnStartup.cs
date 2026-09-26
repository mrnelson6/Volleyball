using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Volleyball.EditorTools
{
    /// <summary>
    /// Opening the project lands on the main menu scene instead of whatever the last session
    /// (or a batch World Tour build) left open. Runs once per editor session, so switching to an
    /// arena and recompiling doesn't yank you back. The World Tour build regenerates the menu at
    /// the same path, so opening by path always finds the current one.
    /// </summary>
    [InitializeOnLoad]
    static class OpenMainMenuOnStartup
    {
        const string MenuPath = "Assets/Scenes/MainMenu.unity";
        const string DoneKey = "Volleyball.OpenedMainMenuThisSession";

        static OpenMainMenuOnStartup()
        {
            if (UnityEngine.Application.isBatchMode || SessionState.GetBool(DoneKey, false)) return;
            EditorApplication.delayCall += Open;
        }

        static void Open()
        {
            SessionState.SetBool(DoneKey, true);
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SceneManager.GetActiveScene().path == MenuPath) return;
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) return; // never throw away unsaved work
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuPath) == null) return;
            EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Single);
        }
    }
}
