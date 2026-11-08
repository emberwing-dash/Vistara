#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Vistara.EditorTools
{
    /// <summary>Builds the MainMenu + Workspace scenes (XR rig, neon environment, post-processing, Vistara controllers).</summary>
    public static class VistaraSceneBuilder
    {
        const string RigPrefab = "Assets/VRTemplateAssets/Prefabs/Setup/Complete XR Origin Set Up Variant.prefab";
        const string MatDir = "Assets/Vistara/Materials";
        const string SceneDir = "Assets/Scenes";

        [MenuItem("Vistara/Build Scenes")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(MatDir);
            BuildScene("MainMenu", go => go.AddComponent<MainMenuController>(), 2.1f);
            BuildScene("Workspace", go => go.AddComponent<WorkspaceController>(), 2.3f);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(SceneDir + "/MainMenu.unity", true),
                new EditorBuildSettingsScene(SceneDir + "/Workspace.unity", true),
            };
            EditorSceneManager.OpenScene(SceneDir + "/MainMenu.unity");
            AssetDatabase.SaveAssets();
            Debug.Log("[Vistara] Scenes built: MainMenu, Workspace");
        }

        static void BuildScene(string name, System.Action<GameObject> addController, float distance)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- XR rig (XR Interaction Simulator auto-loads when enabled in XRI settings) ----
            var rigAsset = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefab);
            GameObject rig = null;
            if (rigAsset != null) rig = (GameObject)PrefabUtility.InstantiatePrefab(rigAsset);
            else Debug.LogError("[Vistara] XR rig prefab missing: " + RigPrefab);
            if (rig != null) rig.transform.position = Vector3.zero;

            // ---- event system with XR UI input ----
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(XRUIInputModule));

            // ---- camera look ----
            var cam = Camera.main;
            if (cam == null && rig != null) cam = rig.GetComponentInChildren<Camera>(true);
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Theme.Bg;
                cam.nearClipPlane = 0.05f;
                var ud = cam.GetUniversalAdditionalCameraData();
                ud.renderPostProcessing = true;
            }

            // ---- lighting / fog ----
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.18f, 0.32f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Theme.Bg; RenderSettings.fogDensity = 0.028f;

            BuildEnvironment();
            BuildVolume();

            // ---- Vistara UI rig ----
            var root = new GameObject("VistaraUI");
            root.transform.position = new Vector3(0, 1.45f, distance);
            var pf = root.AddComponent<PlaceInFront>(); pf.distance = distance; pf.heightOffset = name == "MainMenu" ? -0.05f : 0.05f;
            addController(root);

            EditorSceneManager.SaveScene(scene, $"{SceneDir}/{name}.unity");
        }

        // ------------------------------------------------------------------ environment
        static Material NeonMat(string name, Color c, float intensity)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, path); }
            m.shader = Shader.Find("Universal Render Pipeline/Unlit");
            m.SetColor("_BaseColor", new Color(c.r * intensity, c.g * intensity, c.b * intensity, 1f));   // HDR value -> bloom
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material GridMat()
        {
            string tp = "Assets/Vistara/Materials/GridTex.png";
            if (!File.Exists(tp))
            {
                var t = new Texture2D(256, 256, TextureFormat.RGBA32, false);
                for (int y = 0; y < 256; y++)
                    for (int x = 0; x < 256; x++)
                    {
                        bool line = x < 3 || y < 3;
                        t.SetPixel(x, y, line ? new Color(0.10f, 0.75f, 0.95f, 1f) : new Color(0.02f, 0.03f, 0.08f, 1f));
                    }
                File.WriteAllBytes(tp, t.EncodeToPNG());
                AssetDatabase.ImportAsset(tp);
                var imp = (TextureImporter)AssetImporter.GetAtPath(tp);
                imp.wrapMode = TextureWrapMode.Repeat; imp.mipmapEnabled = true; imp.anisoLevel = 8; imp.SaveAndReimport();
            }
            string mp = MatDir + "/Grid.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, mp); }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
            m.SetTexture("_BaseMap", tex); m.mainTextureScale = new Vector2(40, 40);
            m.SetColor("_BaseColor", new Color(0.8f, 0.9f, 1f));
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material DarkMat()
        {
            string p = MatDir + "/Dark.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, p); }
            m.SetColor("_BaseColor", new Color(0.03f, 0.04f, 0.09f)); m.SetFloat("_Smoothness", 0.7f); m.SetFloat("_Metallic", 0.6f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static GameObject Cube(Transform parent, string n, Vector3 pos, Vector3 scale, Material m, Vector3? euler = null, bool collider = false)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = n; g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localScale = scale;
            if (euler.HasValue) g.transform.localEulerAngles = euler.Value;
            g.GetComponent<Renderer>().sharedMaterial = m;
            if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

        static void BuildEnvironment()
        {
            var env = new GameObject("Environment").transform;
            var cyan = NeonMat("NeonCyan", Theme.Cyan, 1.5f);
            var mag = NeonMat("NeonMagenta", Theme.Magenta, 1.5f);
            var vio = NeonMat("NeonViolet", Theme.Violet, 1.4f);
            var dark = DarkMat();

            // floor + ceiling + walls (a neon corridor facing +Z)
            // the rig has gravity (XRI locomotion): the floor MUST keep its collider or the user falls forever
            var floor = Cube(env, "Floor", new Vector3(0, -0.05f, 8), new Vector3(10, 0.1f, 40), GridMat(), null, true);
            Cube(env, "Ceiling", new Vector3(0, 4.2f, 8), new Vector3(10, 0.1f, 40), dark);
            Cube(env, "WallL", new Vector3(-5, 2.1f, 8), new Vector3(0.1f, 4.4f, 40), dark);
            Cube(env, "WallR", new Vector3(5, 2.1f, 8), new Vector3(0.1f, 4.4f, 40), dark);
            Cube(env, "WallBack", new Vector3(0, 2.1f, -5), new Vector3(10, 4.4f, 0.1f), dark);
            Cube(env, "WallFront", new Vector3(0, 2.1f, 28), new Vector3(10, 4.4f, 0.1f), dark);

            // repeating neon frames along the corridor
            for (int i = 0; i < 12; i++)
            {
                float z = -2f + i * 2.6f;
                var m = i % 3 == 0 ? mag : (i % 3 == 1 ? cyan : vio);
                float w = 4.75f, h = 3.95f, t = 0.035f;
                Cube(env, "FrameTop" + i, new Vector3(0, h + 0.1f, z), new Vector3(w * 2, t, t), m);
                Cube(env, "FrameL" + i, new Vector3(-w, h / 2 + 0.1f, z), new Vector3(t, h, t), m);
                Cube(env, "FrameR" + i, new Vector3(w, h / 2 + 0.1f, z), new Vector3(t, h, t), m);
                Cube(env, "FrameBottom" + i, new Vector3(0, 0.02f, z), new Vector3(w * 2, t, t), m);
            }
            // long guide strips + angled accents
            for (int s = -1; s <= 1; s += 2)
            {
                Cube(env, "StripLow" + s, new Vector3(s * 4.9f, 0.5f, 10), new Vector3(0.05f, 0.05f, 40), cyan);
                Cube(env, "StripHigh" + s, new Vector3(s * 4.9f, 3.4f, 10), new Vector3(0.05f, 0.05f, 40), mag);
                Cube(env, "Diag" + s, new Vector3(s * 4.9f, 2.0f, 6 + s), new Vector3(0.05f, 0.05f, 6), vio, new Vector3(0, 0, s * 38));
            }

            // floating particles
            var ps = new GameObject("Dust", typeof(ParticleSystem)).GetComponent<ParticleSystem>();
            ps.transform.SetParent(env, false); ps.transform.localPosition = new Vector3(0, 2, 8);
            var main = ps.main; main.startLifetime = 14f; main.startSpeed = 0.06f; main.startSize = 0.03f; main.maxParticles = 400;
            main.startColor = new ParticleSystem.MinMaxGradient(Theme.Cyan, Theme.Magenta); main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 30;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(9, 4, 30);
            var pr = ps.GetComponent<ParticleSystemRenderer>();
            string tp = MatDir + "/Dot.png";
            if (!File.Exists(tp))
            {
                var t = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) { float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 32f; t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(1.2f - d * 1.2f))); }
                File.WriteAllBytes(tp, t.EncodeToPNG()); AssetDatabase.ImportAsset(tp);
            }
            string pm = MatDir + "/Particle.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(pm);
            if (mat == null) { mat = new Material(Shader.Find("Sprites/Default")); AssetDatabase.CreateAsset(mat, pm); }
            mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
            pr.sharedMaterial = mat;

            // soft key light
            var l = new GameObject("KeyLight", typeof(Light)).GetComponent<Light>();
            l.type = LightType.Directional; l.color = new Color(0.55f, 0.6f, 1f); l.intensity = 0.6f; l.transform.rotation = Quaternion.Euler(50, 20, 0);
        }

        static void BuildVolume()
        {
            string vp = MatDir + "/VistaraVolume.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(vp);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, vp);
                var bloom = profile.Add<Bloom>(true);
                bloom.threshold.Override(0.85f); bloom.intensity.Override(1.35f); bloom.scatter.Override(0.72f);
                var vig = profile.Add<Vignette>(true); vig.intensity.Override(0.28f);
                var ca = profile.Add<ColorAdjustments>(true); ca.postExposure.Override(0.1f); ca.contrast.Override(8f);
                EditorUtility.SetDirty(profile);
            }
            var go = new GameObject("PostFX", typeof(Volume));
            var v = go.GetComponent<Volume>(); v.isGlobal = true; v.sharedProfile = profile; v.priority = 10;
        }
    }
}
#endif
