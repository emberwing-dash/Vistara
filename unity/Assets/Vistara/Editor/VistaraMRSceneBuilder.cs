#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Vistara.EditorTools
{
    /// <summary>
    /// Builds the Vistara MainMenu + Workspace scenes inside the MR project. The XR rig is the template's own
    /// "MR Interaction Setup" prefab (passthrough camera, hands, controllers, XR Interaction Simulator), so the MR behaviour is kept;
    /// the original SampleScene is left untouched. Menu: Vistara > Build MR Scenes   (batch: -executeMethod Vistara.EditorTools.VistaraMRSceneBuilder.BuildAll)
    /// </summary>
    public static class VistaraMRSceneBuilder
    {
        const string RigPrefab = "Assets/MRTemplateAssets/Prefabs/MR Interaction Setup.prefab";
        const string PermissionsPrefab = "Assets/MRTemplateAssets/Prefabs/Permissions Manager Variant.prefab";
        const string SceneDir = "Assets/Scenes";

        [MenuItem("Vistara/Build MR Scenes")]
        public static void BuildAll()
        {
            BuildScene("MainMenu", go => go.AddComponent<MainMenuController>(), 1.6f);
            BuildScene("Workspace", go => go.AddComponent<WorkspaceController>(), 1.9f);
            BuildScene("Journey", go => go.AddComponent<JourneyController>(), 1.9f);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(SceneDir + "/MainMenu.unity", true),
                new EditorBuildSettingsScene(SceneDir + "/Workspace.unity", true),
                new EditorBuildSettingsScene(SceneDir + "/Journey.unity", true),
                new EditorBuildSettingsScene(SceneDir + "/SampleScene.unity", true),
            };
            EditorSceneManager.OpenScene(SceneDir + "/MainMenu.unity");
            AssetDatabase.SaveAssets();
            Debug.Log("[Vistara] MR scenes built: MainMenu, Workspace (SampleScene kept)");
        }

        static void BuildScene(string name, System.Action<GameObject> addController, float distance)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- MR rig from the template (XR Origin + AR/passthrough camera + hands/controllers + EventSystem) ----
            var rigAsset = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefab);
            if (rigAsset == null) { Debug.LogError("[Vistara] MR rig prefab missing: " + RigPrefab); return; }
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigAsset);
            rig.transform.position = Vector3.zero;
            // the template tutorial (coaching goals) needs scene-specific UI wiring that the Vistara scenes do not use
            foreach (var t in rig.GetComponentsInChildren<Transform>(true))
                if (t.name == "Goal Manager") t.gameObject.SetActive(false);

            // template components that need scene-specific UI wiring (tutorial goals, object spawner UI, occlusion settings) are switched off
            var perms = AssetDatabase.LoadAssetAtPath<GameObject>(PermissionsPrefab);
            if (perms != null) PrefabUtility.InstantiatePrefab(perms);
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (mb != null && (mb.GetType().Name == "OcclusionManager" || mb.GetType().Name == "SpawnedObjectsManager" || mb.GetType().Name == "GoalManager"))
                    mb.enabled = false;

            // ---- EventSystem must use the XR UI input module so controller rays / hands can click world-space canvases ----
            var es = Object.FindFirstObjectByType<EventSystem>();
            if (es == null) es = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
            foreach (var m in es.GetComponents<BaseInputModule>().Where(m => !(m is XRUIInputModule)).ToArray()) Object.DestroyImmediate(m);
            if (es.GetComponent<XRUIInputModule>() == null) es.gameObject.AddComponent<XRUIInputModule>();

            var l = new GameObject("KeyLight", typeof(Light)).GetComponent<Light>();
            l.type = LightType.Directional; l.color = new Color(0.9f, 0.95f, 1f); l.intensity = 0.8f; l.transform.rotation = Quaternion.Euler(50, 20, 0);

            // ---- Vistara UI (code-built, world-space, placed in front of the user once the HMD pose is valid) ----
            var root = new GameObject("VistaraUI");
            root.transform.position = new Vector3(0, 1.45f, distance);
            var pf = root.AddComponent<PlaceInFront>(); pf.distance = distance; pf.heightOffset = name == "MainMenu" ? -0.05f : 0.05f;
            addController(root);

            Debug.Log("[Vistara] rig children: " + string.Join(", ", Enumerable.Range(0, rig.transform.childCount).Select(i => rig.transform.GetChild(i).name)));
            EditorSceneManager.SaveScene(scene, $"{SceneDir}/{name}.unity");
        }
    }
}
#endif
