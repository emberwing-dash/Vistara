#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Vistara.EditorTools
{
    /// <summary>Pressing Play always starts in the main menu (the categories only show after START), whichever scene is open. Automated preview runs (-vistara... / -preview... arguments) are left alone.</summary>
    [InitializeOnLoad]
    static class PlayFromMenu
    {
        static PlayFromMenu()
        {
            foreach (var a in Environment.GetCommandLineArgs()) { var l = a.ToLowerInvariant(); if (l.StartsWith("-vistara") || l.StartsWith("-preview")) { EditorSceneManager.playModeStartScene = null; return; } }
            var menu = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/MainMenu.unity");
            if (menu != null) EditorSceneManager.playModeStartScene = menu;
        }
    }
}
#endif
