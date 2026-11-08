#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Vistara.EditorTools
{
    /// <summary>When launched with -vistaraSmoke, opens MainMenu and enters play mode (used by the automated smoke test).</summary>
    [InitializeOnLoad]
    static class SmokeLauncher
    {
        static bool started;
        static SmokeLauncher()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-vistaraSmoke") < 0) return;
            Debug.Log("[Smoke] launcher armed");
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (started || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) { started = true; return; }
            started = true;
            Debug.Log("[Smoke] opening MainMenu and entering play mode");
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            EditorApplication.isPlaying = true;
        }
    }
}
#endif
