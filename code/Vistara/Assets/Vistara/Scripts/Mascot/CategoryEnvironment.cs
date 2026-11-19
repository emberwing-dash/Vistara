using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Vistara
{
    /// <summary>
    /// A stylised low-poly 3D world that matches the category you pick (kitchen, bike road, boxing ring, ski slope, underwater ...).
    /// Built at runtime from primitives, so there are no extra assets: sky dome, ground, scenery, fog, light colour and a little life (drifting clouds, bubbles, swaying kelp ...).
    /// It replaces the neon environment of the base scene while a category is chosen.
    /// </summary>
    public class CategoryEnvironment : MonoBehaviour
    {
        public static CategoryEnvironment Instance { get; private set; }
        Transform root; string shown; System.Random rng = new System.Random(7);
        struct Anim { public Transform t; public int kind; public Vector3 a; public float speed, phase; public Vector3 basePos; }
        readonly List<Anim> anims = new List<Anim>();
        readonly Dictionary<Color, Material> mats = new Dictionary<Color, Material>();
        Mesh coneMesh; Shader litSh, skySh;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => { if (s.name == "Journey" || s.name == "Workspace") Spawn(); };
            var n = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name; if (n == "Journey" || n == "Workspace") Spawn();
        }
        static void Spawn() { if (Instance == null) new GameObject("CategoryWorld").AddComponent<CategoryEnvironment>(); }

        void Awake()
        {
            Instance = this;
            // The category worlds are visual only (props have no colliders) and hide the base room, so keep one invisible floor under the rig - without it the XR rig's gravity makes you fall.
            var floor = new GameObject("SafeFloor"); floor.transform.SetParent(transform, false); floor.transform.localPosition = new Vector3(0, -0.5f, 0);
            var bc = floor.AddComponent<BoxCollider>(); bc.size = new Vector3(400f, 1f, 400f);
            Init();
        }
        void Init() { litSh = Shader.Find("Universal Render Pipeline/Lit"); skySh = Shader.Find("Vistara/SkyGradient"); coneMesh = MakeCone(18); }
        void OnDestroy() { if (Instance == this) Instance = null; }
        /// <summary>The pieces of the current world (props / primitives), used by the spatial shift to move the whole environment.</summary>
        public IEnumerable<Transform> WarpTargets()
        {
            if (root == null) yield break;
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i); if (c.name == "Sky") continue;
                if (c.name == "BlenderWorld") { for (int k = 0; k < c.childCount; k++) yield return c.GetChild(k); }
                else yield return c;
            }
        }

        void Update()
        {
            string cat = AppState.Category;
            if (cat != shown && !string.IsNullOrEmpty(cat))
            {
                bool first = shown == null; shown = cat; string target = cat;
                if (!first && SpaceShift.Instance != null && !SpaceShift.Instance.Busy) StartCoroutine(SpaceShift.Instance.Run(() => Build(target)));   // spatial shift into the new world
                else Build(cat);
            }
            float t = Time.time;
            for (int i = 0; i < anims.Count; i++)
            {
                var a = anims[i]; if (a.t == null) continue;
                switch (a.kind)
                {
                    case 0: { var p = a.t.position; p.x += a.speed * Time.deltaTime; if (p.x > 60f) p.x = -60f; a.t.position = p; break; }                         // drifting cloud
                    case 1: a.t.position = a.basePos + Vector3.up * Mathf.Sin(t * a.speed + a.phase) * a.a.y; break;                                                  // bobbing
                    case 2: { var p = a.t.position; p.y += a.speed * Time.deltaTime; if (p.y > a.a.y) p.y = a.basePos.y; a.t.position = p; break; }                // rising bubble
                    case 3: a.t.localRotation = Quaternion.Euler(Mathf.Sin(t * a.speed + a.phase) * 8f, 0, Mathf.Sin(t * a.speed * 0.7f + a.phase) * 8f); break;     // swaying kelp
                    case 4: { float ang = t * a.speed + a.phase; a.t.position = a.basePos + new Vector3(Mathf.Cos(ang) * a.a.x, 0, Mathf.Sin(ang) * a.a.x); a.t.rotation = Quaternion.Euler(0, -ang * Mathf.Rad2Deg, 0); break; } // circling fish
                }
            }
        }

        // ---------------------------------------------------------------- helpers
        Material M(Color c, float smooth = 0.08f)
        {
            if (mats.TryGetValue(c, out var m)) return m;
            m = new Material(litSh != null ? litSh : Shader.Find("Standard")); m.SetColor("_BaseColor", c); m.color = c; m.SetFloat("_Smoothness", smooth); mats[c] = m; return m;
        }
        static Color C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
        double R() { return rng.NextDouble(); }
        float Rf(float a, float b) { return a + (float)R() * (b - a); }

        GameObject P(PrimitiveType type, Vector3 pos, Vector3 scale, Color c, Vector3? euler = null, Transform parent = null)
        {
            var g = GameObject.CreatePrimitive(type); var col = g.GetComponent<Collider>(); if (col != null) Destroy(col);
            g.transform.SetParent(parent != null ? parent : root, false); g.transform.localPosition = pos; g.transform.localScale = scale;
            if (euler.HasValue) g.transform.localRotation = Quaternion.Euler(euler.Value);
            g.GetComponent<Renderer>().sharedMaterial = M(c); return g;
        }
        GameObject Box(Vector3 pos, Vector3 size, Color c, Vector3? e = null) { return P(PrimitiveType.Cube, pos, size, c, e); }
        GameObject Ball(Vector3 pos, Vector3 size, Color c) { return P(PrimitiveType.Sphere, pos, size, c); }
        GameObject Cyl(Vector3 pos, float r, float h, Color c, Vector3? e = null) { return P(PrimitiveType.Cylinder, pos, new Vector3(r * 2, h / 2, r * 2), c, e); }
        GameObject Cone(Vector3 pos, float r, float h, Color c)
        {
            var g = new GameObject("Cone"); g.transform.SetParent(root, false); g.transform.localPosition = pos; g.transform.localScale = new Vector3(r * 2, h, r * 2);
            g.AddComponent<MeshFilter>().sharedMesh = coneMesh; g.AddComponent<MeshRenderer>().sharedMaterial = M(c); return g;
        }
        static Mesh MakeCone(int n)
        {
            var v = new List<Vector3>(); var t = new List<int>(); v.Add(new Vector3(0, 1, 0)); v.Add(new Vector3(0, 0, 0));
            for (int i = 0; i < n; i++) { float a = i * Mathf.PI * 2 / n; v.Add(new Vector3(Mathf.Cos(a) * 0.5f, 0, Mathf.Sin(a) * 0.5f)); }
            for (int i = 0; i < n; i++) { int a = 2 + i, b = 2 + (i + 1) % n; t.Add(0); t.Add(b); t.Add(a); t.Add(1); t.Add(a); t.Add(b); }
            var m = new Mesh(); m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }
        void Add(Transform t, int kind, float speed, Vector3 a, float phase = 0f) { anims.Add(new Anim { t = t, kind = kind, speed = speed, a = a, phase = phase, basePos = t.position }); }
        void Ground(Color c, float r = 60f, float y = 0f) { Cyl(new Vector3(0, y - 0.05f, 0), r, 0.1f, c); }
        void Tree(Vector3 p, float s, Color trunk, Color leaf) { Cyl(p + new Vector3(0, 0.6f * s, 0), 0.12f * s, 1.2f * s, trunk); Cone(p + new Vector3(0, 0.9f * s, 0), 1.0f * s, 1.6f * s, leaf); Cone(p + new Vector3(0, 1.6f * s, 0), 0.75f * s, 1.3f * s, leaf); }
        void Clouds(int n, float y0, float y1, float spread)
        {
            for (int i = 0; i < n; i++)
            {
                var c = new GameObject("Cloud").transform; c.SetParent(root, false); c.localPosition = new Vector3(Rf(-spread, spread), Rf(y0, y1), Rf(8, spread));
                for (int k = 0; k < 4; k++) { var b = Ball(new Vector3(k * 1.4f - 2f, Rf(-0.2f, 0.4f), Rf(-0.5f, 0.5f)), Vector3.one * Rf(1.4f, 2.4f), Color.white); b.transform.SetParent(c, false); b.transform.localPosition = new Vector3(k * 1.4f - 2f, Rf(-0.2f, 0.4f), Rf(-0.5f, 0.5f)); }
                Add(c, 0, Rf(0.3f, 0.9f), Vector3.zero);
            }
        }

        // ---------------------------------------------------------------- one world per category
        class Look { public Color top, horizon, bottom, fog, ambient, light; public float fogDensity = 0.012f; public bool hideGround; }

        void Build(string cat)
        {
            if (litSh == null) return;
            if (root != null) Destroy(root.gameObject);
            anims.Clear(); mats.Clear(); rng = new System.Random(cat.GetHashCode() & 0x7fffffff);
            root = new GameObject("World_" + cat).transform; root.SetParent(transform, false);
            var neon = GameObject.Find("Environment"); if (neon != null) neon.SetActive(false);            // the neon room of the base scene
            var dust = GameObject.Find("Dust"); if (dust != null) dust.SetActive(false);
            var look = new Look { top = C("#4f8de8"), horizon = C("#cfe6ff"), bottom = C("#6f8fa0"), fog = C("#cfe6ff"), ambient = C("#bfcde8"), light = Color.white };
            switch (cat)
            {
                case "Kitchen": Kitchen(look); break;
                case "Bike": Road(look, false); break;
                case "BikePolo": Road(look, true); break;
                case "Boxing": Boxing(look); break;
                case "Horse": Horse(look); break;
                case "Jumping": case "Parkour": Rooftops(look); break;
                case "Longboarding": case "Skate": SkatePark(look); break;
                case "Motor": Track(look); break;
                case "Plane": Sky(look); break;
                case "RockClimbing": Cliffs(look); break;
                case "Scuba": Underwater(look); break;
                case "Ski": Snow(look); break;
                case "Surfing": Beach(look); break;
                default: Ground(C("#6fbf73")); break;
            }
            LoadWorld(cat);                     // the Blender-made world (assets/categories/<cat>) replaces the primitive stand-in; the sky/fog/light look below is kept
            Apply(look);
        }

        /// <summary>Instantiates Resources/Categories/&lt;cat&gt;_World (exported from assets/categories/&lt;cat&gt;/&lt;cat&gt;.blend), recolours it from the material names (M_rrggbb) and animates the living bits by object name.</summary>
        void LoadWorld(string cat)
        {
            var prefab = Resources.Load<GameObject>("Categories/" + cat + "_World");
            if (prefab == null) return;
            for (int i = root.childCount - 1; i >= 0; i--) Destroy(root.GetChild(i).gameObject);
            anims.Clear();
            var w = Instantiate(prefab, root); w.name = "BlenderWorld"; w.transform.localPosition = Vector3.zero; w.transform.localRotation = Quaternion.identity; w.transform.localScale = Vector3.one;
            foreach (var r in w.GetComponentsInChildren<Renderer>())
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++) ms[i] = M(ColorOf(ms[i] != null ? ms[i].name : ""));
                r.sharedMaterials = ms; r.shadowCastingMode = ShadowCastingMode.Off;
            }
            foreach (var t in w.GetComponentsInChildren<Transform>())
            {
                if (t == w.transform) continue; string n = t.name;
                if (n.StartsWith("Cloud")) Add(t, 0, Rf(0.3f, 0.9f), Vector3.zero);
                else if (n.StartsWith("Bubble")) Add(t, 2, Rf(0.4f, 0.9f), new Vector3(0, t.position.y + 7f, 0));
                else if (n.StartsWith("Kelp") || n.StartsWith("Seaweed")) Add(t, 3, Rf(0.8f, 1.4f), Vector3.zero, Rf(0f, 6f));
                else if (n.StartsWith("Fish")) Add(t, 4, Rf(0.25f, 0.6f), new Vector3(Rf(1.2f, 2.6f), 0, 0), Rf(0f, 6f));
                else if (n.StartsWith("Balloon") || n.StartsWith("Bird") || n.StartsWith("Buoy") || n.StartsWith("Wave")) Add(t, 1, Rf(0.6f, 1.2f), new Vector3(0, n.StartsWith("Wave") ? 0.12f : 0.4f, 0), Rf(0f, 6f));
            }
        }
        static Color ColorOf(string matName)
        {
            var m = System.Text.RegularExpressions.Regex.Match(matName ?? "", "M_([0-9a-fA-F]{6})");
            return m.Success && ColorUtility.TryParseHtmlString("#" + m.Groups[1].Value, out var c) ? c : new Color(0.75f, 0.75f, 0.78f);
        }

        void Apply(Look l)
        {
            if (skySh != null)
            {
                var dome = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(dome.GetComponent<Collider>()); dome.name = "Sky";
                dome.transform.SetParent(root, false); dome.transform.localScale = Vector3.one * 180f;
                var m = new Material(skySh); m.SetColor("_Top", l.top); m.SetColor("_Horizon", l.horizon); m.SetColor("_Bottom", l.bottom); dome.GetComponent<Renderer>().sharedMaterial = m;
            }
            RenderSettings.skybox = null; RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = l.ambient;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogColor = l.fog; RenderSettings.fogDensity = l.fogDensity;
            var cam = Camera.main; if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = l.horizon; }
            var key = GameObject.Find("KeyLight"); if (key != null) { var lt = key.GetComponent<Light>(); if (lt != null) { lt.color = l.light; lt.intensity = 1.1f; } }
        }

        void Kitchen(Look l)
        {
            l.top = l.horizon = C("#f6ead8"); l.bottom = C("#c9b79b"); l.fog = C("#f1e3cf"); l.ambient = C("#fff0dc"); l.light = C("#fff4e0"); l.fogDensity = 0.004f;
            Color tile1 = C("#f2efe9"), tile2 = C("#c9d6d6");
            for (int x = -5; x < 5; x++) for (int z = -3; z < 8; z++) Box(new Vector3(x + 0.5f, -0.01f, z + 0.5f), new Vector3(1, 0.02f, 1), ((x + z) & 1) == 0 ? tile1 : tile2);
            Box(new Vector3(0, 2, 7.6f), new Vector3(10, 4, 0.2f), C("#ead9bd")); Box(new Vector3(-5, 2, 2.5f), new Vector3(0.2f, 4, 10), C("#e6d2b3")); Box(new Vector3(5, 2, 2.5f), new Vector3(0.2f, 4, 10), C("#e6d2b3"));
            Box(new Vector3(0, 4.05f, 2.5f), new Vector3(10, 0.2f, 10), C("#fff8ee"));
            Box(new Vector3(0, 0.45f, 7.1f), new Vector3(8.4f, 0.9f, 0.8f), C("#4aa6a0")); Box(new Vector3(0, 0.93f, 7.1f), new Vector3(8.5f, 0.06f, 0.9f), C("#f4f1ec"));
            Box(new Vector3(0, 2.7f, 7.3f), new Vector3(8.4f, 0.9f, 0.5f), C("#4aa6a0")); Box(new Vector3(3f, 2.0f, 7.45f), new Vector3(2.2f, 1.2f, 0.05f), C("#bfe6ff"));
            Box(new Vector3(0, 0.45f, 4.6f), new Vector3(2.4f, 0.9f, 1.1f), C("#d9a066")); Box(new Vector3(0, 0.93f, 4.6f), new Vector3(2.5f, 0.06f, 1.2f), C("#f4f1ec"));
            Box(new Vector3(-3.9f, 1f, 6.8f), new Vector3(1.1f, 2f, 0.9f), C("#d7dde3"));
            for (int i = 0; i < 4; i++) { Cyl(new Vector3(-1.5f + i * 1.0f, 1.55f, 7.05f), 0.18f, 0.08f, C("#3a3a44"), new Vector3(90, 0, 0)); Box(new Vector3(-1.5f + i * 1.0f, 1.95f, 7.1f), new Vector3(0.03f, 0.7f, 0.03f), C("#b8bcc6")); }
            Cyl(new Vector3(-0.3f, 1.02f, 7.1f), 0.2f, 0.2f, C("#b8bcc6")); Cyl(new Vector3(0.6f, 1.0f, 7.1f), 0.14f, 0.16f, C("#ff8a2a"));
        }

        void Road(Look l, bool polo)
        {
            l.top = C("#3f86e6"); l.horizon = C("#d6ecff"); l.fog = C("#d6ecff"); l.ambient = C("#c6d6ee"); l.fogDensity = 0.01f;
            Ground(C("#7cc47a")); Box(new Vector3(0, 0.0f, 25), new Vector3(5.2f, 0.04f, 70), C("#3c3f48"));
            for (int i = 0; i < 18; i++) Box(new Vector3(0, 0.03f, -2 + i * 3.6f), new Vector3(0.14f, 0.02f, 1.6f), Color.white);
            for (int i = 0; i < 26; i++) { float z = Rf(-4, 55); float s = Rf(0.9f, 1.6f); Tree(new Vector3(-Rf(4.5f, 11), 0, z), s, C("#7a5a3a"), C(i % 3 == 0 ? "#2f9b57" : "#3dbb6a")); Tree(new Vector3(Rf(4.5f, 11), 0, z + 1.5f), s, C("#7a5a3a"), C("#3dbb6a")); }
            for (int i = 0; i < 6; i++) Ball(new Vector3(Rf(-40, 40), -4f, Rf(45, 80)), new Vector3(Rf(16, 30), Rf(12, 22), Rf(16, 30)), C(i % 2 == 0 ? "#6fb37a" : "#5aa56a"));
            if (polo) { Box(new Vector3(0, 0.02f, 9), new Vector3(9, 0.03f, 14), C("#4d8f5a")); for (int i = 0; i < 2; i++) { float z = 2.5f + i * 13f; Box(new Vector3(-1.2f, 0.7f, z), new Vector3(0.1f, 1.4f, 0.1f), Color.white); Box(new Vector3(1.2f, 0.7f, z), new Vector3(0.1f, 1.4f, 0.1f), Color.white); Box(new Vector3(0, 1.4f, z), new Vector3(2.5f, 0.1f, 0.1f), Color.white); } }
            Ball(new Vector3(25, 40, 90), Vector3.one * 7f, C("#fff3b0")); Clouds(7, 22, 34, 70);
        }

        void Boxing(Look l)
        {
            l.top = C("#0c1226"); l.horizon = C("#1d2547"); l.bottom = C("#0a0d1a"); l.fog = C("#10152c"); l.ambient = C("#6a74a8"); l.light = C("#ffe9c8"); l.fogDensity = 0.02f;
            Ground(C("#1b1e2c")); var ring = new Vector3(0, 0, 4.2f);
            Box(ring + new Vector3(0, 0.45f, 0), new Vector3(6, 0.9f, 6), C("#2b2f4a")); Box(ring + new Vector3(0, 0.93f, 0), new Vector3(5.6f, 0.06f, 5.6f), C("#3a64c9"));
            foreach (var c in new[] { new Vector2(-2.7f, -2.7f), new Vector2(2.7f, -2.7f), new Vector2(-2.7f, 2.7f), new Vector2(2.7f, 2.7f) }) Cyl(ring + new Vector3(c.x, 1.55f, c.y), 0.09f, 1.3f, C("#d9d9e3"));
            Color[] rope = { C("#e04848"), Color.white, C("#3b78e0") };
            for (int r = 0; r < 3; r++) { float y = 1.15f + r * 0.35f; Cyl(ring + new Vector3(0, y, -2.7f), 0.03f, 5.4f, rope[r], new Vector3(0, 0, 90)); Cyl(ring + new Vector3(0, y, 2.7f), 0.03f, 5.4f, rope[r], new Vector3(0, 0, 90)); Cyl(ring + new Vector3(-2.7f, y, 0), 0.03f, 5.4f, rope[r], new Vector3(90, 0, 0)); Cyl(ring + new Vector3(2.7f, y, 0), 0.03f, 5.4f, rope[r], new Vector3(90, 0, 0)); }
            for (int i = 0; i < 4; i++) { var sp = Cone(new Vector3(-4.5f + i * 3f, 7f, 5f), 1.6f, 6.5f, C("#fff1c2")); sp.transform.localRotation = Quaternion.Euler(180, 0, 0); sp.transform.localPosition = new Vector3(-4.5f + i * 3f, 7f, 5f); }
            for (int i = 0; i < 40; i++) { float a = Rf(0, Mathf.PI * 2); float r = Rf(11, 18); Box(new Vector3(Mathf.Cos(a) * r, 1f, 4f + Mathf.Sin(a) * r), new Vector3(0.7f, Rf(1.4f, 1.9f), 0.5f), Color.Lerp(C("#2a2e48"), C("#4a3a5a"), (float)R())); }
        }

        void Horse(Look l)
        {
            l.top = C("#4a90e8"); l.horizon = C("#e6f3ff"); l.fog = C("#e6f3ff"); l.ambient = C("#cfe0d0"); l.light = C("#fff6dc"); l.fogDensity = 0.007f;
            Ground(C("#79c26a")); for (int i = 0; i < 40; i++) { float a = i * Mathf.PI * 2 / 40; var p = new Vector3(Mathf.Cos(a) * 9f, 0, 4f + Mathf.Sin(a) * 9f); Box(p + new Vector3(0, 0.55f, 0), new Vector3(0.12f, 1.1f, 0.12f), C("#8a5a34")); Box(p + new Vector3(0, 0.9f, 0), new Vector3(1.4f, 0.09f, 0.09f), C("#a8703f"), new Vector3(0, -a * Mathf.Rad2Deg + 90, 0)); Box(p + new Vector3(0, 0.55f, 0), new Vector3(1.4f, 0.09f, 0.09f), C("#a8703f"), new Vector3(0, -a * Mathf.Rad2Deg + 90, 0)); }
            Box(new Vector3(-8, 2f, 26), new Vector3(7, 4, 6), C("#c0453a")); Box(new Vector3(-8, 4.6f, 26), new Vector3(7.4f, 0.4f, 6.4f), C("#7a2c26")); Box(new Vector3(-8, 4.2f, 26), new Vector3(5, 1.2f, 6.1f), C("#c0453a"), new Vector3(0, 0, 0));
            for (int i = 0; i < 18; i++) Tree(new Vector3(Rf(-30, 30), 0, Rf(14, 44)), Rf(1.2f, 2.2f), C("#7a5a3a"), C("#3dbb6a")); Ball(new Vector3(-30, 42, 90), Vector3.one * 8f, C("#fff3b0")); Clouds(6, 24, 36, 70);
        }

        void Rooftops(Look l)
        {
            l.top = C("#5b3f8f"); l.horizon = C("#ffb070"); l.bottom = C("#3a2a4a"); l.fog = C("#f2a77a"); l.ambient = C("#c9a4a0"); l.light = C("#ffd0a0"); l.fogDensity = 0.012f;
            Box(new Vector3(0, -0.1f, 4), new Vector3(14, 0.2f, 16), C("#6e6a74")); Box(new Vector3(0, 0.15f, 14), new Vector3(14, 0.3f, 2.5f), C("#8a8590"));
            Box(new Vector3(-3, 0.4f, 9), new Vector3(2.5f, 0.8f, 1.2f), C("#a8a2b0")); Box(new Vector3(3.5f, 0.7f, 7.5f), new Vector3(1.4f, 1.4f, 1.4f), C("#9a94a4")); Box(new Vector3(0, 0.55f, 11.5f), new Vector3(0.1f, 0.1f, 6f), C("#c8c8d0"));
            for (int i = 0; i < 36; i++) { float x = Rf(-45, 45), z = Rf(14, 70); float h = Rf(4, 26); Color c = Color.Lerp(C("#3d3552"), C("#6a5a86"), (float)R()); Box(new Vector3(x, h / 2 - 2, z), new Vector3(Rf(3, 6), h + 4, Rf(3, 6)), c); }
            Ball(new Vector3(0, 12, 120), Vector3.one * 16f, C("#ffcf7a"));
        }

        void SkatePark(Look l)
        {
            l.top = C("#4b97ec"); l.horizon = C("#e4f1ff"); l.fog = C("#e4f1ff"); l.ambient = C("#cfd6e2"); l.fogDensity = 0.009f;
            Ground(C("#b9bcc4")); Box(new Vector3(0, 0.0f, 9), new Vector3(16, 0.06f, 22), C("#a6a9b2"));
            for (int s = -1; s <= 1; s += 2) { Box(new Vector3(s * 6f, 0.9f, 10), new Vector3(0.5f, 1.8f, 7f), C("#9a9da6")); Box(new Vector3(s * 4.6f, 0.65f, 10), new Vector3(2.8f, 0.2f, 7f), C("#8e919a"), new Vector3(0, 0, -s * 24)); }
            Box(new Vector3(0, 0.35f, 14), new Vector3(3, 0.7f, 2.2f), C("#c9ccd4")); for (int i = 0; i < 4; i++) Box(new Vector3(-1.4f + i * 0.9f, 0.25f + i * 0.16f, 6.5f), new Vector3(0.9f, 0.5f + i * 0.32f, 2.4f), C("#b0b3bc"));
            Box(new Vector3(-3, 0.45f, 8), new Vector3(0.06f, 0.06f, 4f), C("#e04848")); Cyl(new Vector3(-3, 0.22f, 6.1f), 0.03f, 0.45f, C("#b8bcc6")); Cyl(new Vector3(-3, 0.22f, 9.9f), 0.03f, 0.45f, C("#b8bcc6"));
            Color[] g = { C("#ff7aa8"), C("#ffd23a"), C("#3dbb6a"), C("#3b78e0"), C("#ff8a2a") };
            for (int i = 0; i < 8; i++) Box(new Vector3(-16 + i * 4.6f, 2.5f, 26), new Vector3(4.2f, 5f, 0.4f), g[i % 5]);
            for (int i = 0; i < 12; i++) Tree(new Vector3(Rf(-30, 30), 0, Rf(30, 50)), 1.6f, C("#7a5a3a"), C("#3dbb6a")); Clouds(5, 24, 34, 70);
        }

        void Track(Look l)
        {
            l.top = C("#3f86e6"); l.horizon = C("#dfeeff"); l.fog = C("#dfeeff"); l.ambient = C("#cdd5e4"); l.fogDensity = 0.008f;
            Ground(C("#6fa86a")); Box(new Vector3(0, 0.0f, 30), new Vector3(9, 0.05f, 90), C("#34363e"));
            for (int i = 0; i < 40; i++) { Color c = i % 2 == 0 ? C("#e04848") : Color.white; Box(new Vector3(-4.7f, 0.04f, i * 2.2f - 6), new Vector3(0.5f, 0.06f, 2.2f), c); Box(new Vector3(4.7f, 0.04f, i * 2.2f - 6), new Vector3(0.5f, 0.06f, 2.2f), c); }
            for (int i = 0; i < 10; i++) { var p = new Vector3(i % 2 == 0 ? -6.5f : 6.5f, 0, 6 + i * 6f); for (int k = 0; k < 3; k++) Cyl(p + new Vector3(0, 0.15f + k * 0.3f, 0), 0.42f, 0.3f, k % 2 == 0 ? C("#2a2a30") : Color.white); }
            for (int s = 0; s < 6; s++) Box(new Vector3(-13, 0.6f + s * 0.6f, 14 + s * 0.2f), new Vector3(3, 1.2f, 18), Color.Lerp(C("#4a5a8a"), C("#7a8ac0"), s / 5f));
            for (int i = 0; i < 8; i++) Box(new Vector3(0, 5f, 12 + i * 9f), new Vector3(9.5f, 0.5f, 0.3f), i % 2 == 0 ? C("#ff8a2a") : C("#3b78e0")); Clouds(6, 22, 34, 70);
        }

        void Sky(Look l)
        {
            l.top = C("#2f78e0"); l.horizon = C("#e9f4ff"); l.bottom = C("#cfe6ff"); l.fog = C("#e9f4ff"); l.ambient = C("#d6e4f6"); l.fogDensity = 0.004f;
            Box(new Vector3(0, -45, 40), new Vector3(400, 0.5f, 400), C("#9ccaf2")); Clouds(26, -6, 26, 60);
            for (int i = 0; i < 4; i++) { var pl = new GameObject("Plane").transform; pl.SetParent(root, false); pl.localPosition = new Vector3(Rf(-30, 30), Rf(4, 14), Rf(25, 55)); var b = Box(Vector3.zero, new Vector3(0.5f, 0.5f, 3f), Color.white); b.transform.SetParent(pl, false); var w = Box(Vector3.zero, new Vector3(4f, 0.08f, 0.9f), C("#e04848")); w.transform.SetParent(pl, false); Add(pl, 0, Rf(1.5f, 3f), Vector3.zero); }
            Ball(new Vector3(40, 50, 110), Vector3.one * 9f, C("#fff8c8"));
        }

        void Cliffs(Look l)
        {
            l.top = C("#4b95e6"); l.horizon = C("#e8f2ff"); l.fog = C("#e0ecf8"); l.ambient = C("#cfd0d6"); l.fogDensity = 0.009f;
            Ground(C("#8a7a68")); for (int i = 0; i < 30; i++) { float x = Rf(-40, 40); float h = Rf(6, 34); Box(new Vector3(x, h / 2 - 1, Rf(14, 24)), new Vector3(Rf(4, 9), h, Rf(3, 7)), Color.Lerp(C("#8a7867"), C("#b19c84"), (float)R()), new Vector3(0, Rf(-12, 12), Rf(-5, 5))); }
            for (int i = 0; i < 16; i++) Ball(new Vector3(Rf(-10, 10), 0.3f, Rf(1, 12)), new Vector3(Rf(0.6f, 1.8f), Rf(0.4f, 1.1f), Rf(0.6f, 1.6f)), Color.Lerp(C("#7d7468"), C("#a39a8c"), (float)R()));
            for (int i = 0; i < 8; i++) Ball(new Vector3(Rf(-70, 70), -8f, Rf(70, 110)), new Vector3(Rf(20, 40), Rf(25, 45), Rf(20, 40)), C(i % 2 == 0 ? "#9aa6b8" : "#8794a8")); Clouds(8, 26, 40, 70);
        }

        void Underwater(Look l)
        {
            l.top = C("#0d4f8f"); l.horizon = C("#2aa0c8"); l.bottom = C("#0a3550"); l.fog = C("#1b86b4"); l.ambient = C("#6fc2e6"); l.light = C("#bfeaff"); l.fogDensity = 0.07f;
            Ground(C("#d7c08a"), 60, -2.4f);
            for (int i = 0; i < 30; i++) { Color c = new[] { C("#ff7aa8"), C("#ff8a2a"), C("#e04848"), C("#b46ae0") }[i % 4]; float x = Rf(-9, 9), z = Rf(2, 16); var g = new GameObject("Coral").transform; g.SetParent(root, false); g.localPosition = new Vector3(x, -2.4f, z); for (int k = 0; k < 3; k++) { var b = Cyl(Vector3.zero, 0.1f, Rf(0.6f, 1.6f), c); b.transform.SetParent(g, false); b.transform.localPosition = new Vector3(Rf(-0.3f, 0.3f), 0.5f, Rf(-0.3f, 0.3f)); b.transform.localRotation = Quaternion.Euler(Rf(-18, 18), 0, Rf(-18, 18)); var t = Ball(Vector3.zero, Vector3.one * 0.28f, c); t.transform.SetParent(b.transform, false); t.transform.localPosition = new Vector3(0, 1.05f, 0); t.transform.localScale = new Vector3(0.6f, 0.3f, 0.6f); } }
            for (int i = 0; i < 18; i++) { var k = Cyl(new Vector3(Rf(-10, 10), -0.4f, Rf(4, 18)), 0.06f, 4f, C("#2f9b57")); k.transform.localScale = new Vector3(0.12f, 2f, 0.12f); Add(k.transform, 3, Rf(0.6f, 1.2f), Vector3.zero, Rf(0, 6)); }
            for (int i = 0; i < 40; i++) { var b = Ball(new Vector3(Rf(-6, 6), Rf(-2, 5), Rf(1, 14)), Vector3.one * Rf(0.05f, 0.14f), C("#dff6ff")); Add(b.transform, 2, Rf(0.3f, 0.8f), new Vector3(0, 6f, 0)); }
            for (int i = 0; i < 6; i++) { var f = Ball(new Vector3(0, Rf(0.2f, 2.5f), 8), new Vector3(0.7f, 0.3f, 0.25f), new[] { C("#ffd23a"), C("#ff8a2a"), C("#7ad7ff") }[i % 3]); Add(f.transform, 4, Rf(0.3f, 0.7f), new Vector3(Rf(3, 7), 0, 0), Rf(0, 6)); }
        }

        void Snow(Look l)
        {
            l.top = C("#7fb3e8"); l.horizon = C("#f2f7ff"); l.bottom = C("#e6eef8"); l.fog = C("#eef4fc"); l.ambient = C("#dbe6f6"); l.light = C("#f4f8ff"); l.fogDensity = 0.012f;
            Ground(C("#f7fbff")); for (int i = 0; i < 9; i++) { float x = -80 + i * 20; Cone(new Vector3(x, -2, Rf(60, 90)), Rf(18, 28), Rf(25, 45), i % 2 == 0 ? C("#e9f0f8") : C("#d6e2f0")); Cone(new Vector3(x, 14, Rf(60, 90)), 5, 8, Color.white); }
            for (int i = 0; i < 32; i++) { var p = new Vector3(i % 2 == 0 ? -Rf(4, 12) : Rf(4, 12), 0, Rf(-2, 40)); Cyl(p + new Vector3(0, 0.3f, 0), 0.1f, 0.6f, C("#6a4a2a")); for (int k = 0; k < 3; k++) Cone(p + new Vector3(0, 0.6f + k * 0.7f, 0), 1.2f - k * 0.3f, 1.2f, C("#2d7a4a")); }
            for (int i = 0; i < 10; i++) Ball(new Vector3(Rf(-14, 14), -0.3f, Rf(0, 30)), new Vector3(Rf(1, 3), Rf(0.5f, 1), Rf(1, 3)), Color.white);
        }

        void Beach(Look l)
        {
            l.top = C("#3b78d9"); l.horizon = C("#ffd6a8"); l.bottom = C("#2a8fb0"); l.fog = C("#ffe2bd"); l.ambient = C("#ffe9d0"); l.light = C("#fff0d6"); l.fogDensity = 0.007f;
            Box(new Vector3(0, -0.1f, 40), new Vector3(300, 0.2f, 150), C("#2aa6c4")); Box(new Vector3(0, 0.05f, 1), new Vector3(14, 0.2f, 8), C("#e8cf9a")); Box(new Vector3(0, 0.0f, 3), new Vector3(14, 0.15f, 3), C("#f3dca8"));
            for (int i = 0; i < 26; i++) { var w = Ball(new Vector3(Rf(-40, 40), -0.15f, Rf(8, 70)), new Vector3(Rf(3, 7), 0.5f, Rf(1.5f, 3)), C("#7fd8ea")); Add(w.transform, 1, Rf(0.8f, 1.6f), new Vector3(0, 0.25f, 0), Rf(0, 6)); }
            for (int i = 0; i < 7; i++) { var p = new Vector3(Rf(-7, 7), 0, Rf(-3, 3)); var t = Cyl(p + new Vector3(0, 1.3f, 0), 0.12f, 2.6f, C("#8a5a34"), new Vector3(0, 0, Rf(-10, 10))); for (int k = 0; k < 5; k++) { float a = k * 72f; Box(p + new Vector3(0, 2.7f, 0), new Vector3(1.6f, 0.06f, 0.35f), C("#2f9b57"), new Vector3(0, a, 18)); } }
            Ball(new Vector3(0, 8, 120), Vector3.one * 14f, C("#ffcf7a")); Clouds(6, 20, 32, 70);
        }
    }
}
