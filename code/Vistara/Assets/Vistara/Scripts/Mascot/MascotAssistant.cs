using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Networking;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Vistara
{
    [Serializable] public class OMsg { public string role; public string content; }
    [Serializable] class OOpts { public float temperature = 0.7f; public int num_predict = 150; }
    [Serializable] class OReq { public string model; public OMsg[] messages; public bool stream; public OOpts options; }
    [Serializable] class OResp { public OMsg message; public string error; }

    /// <summary>
    /// Talks to a local Ollama server running Meta's Llama (default llama3.2:3b) at http://127.0.0.1:11434. Nothing leaves the machine.
    /// Start it with `ollama serve` (the component tries to start it itself when it is not running).
    /// </summary>
    public class OllamaClient : MonoBehaviour
    {
        public string baseUrl = "http://127.0.0.1:11434";
        public string model = "llama3.2:3b";
        public bool Online { get; private set; }
        public bool ModelReady { get; private set; }
        bool triedStart;

        public static string EnvOr(string name, string def) { var v = Environment.GetEnvironmentVariable(name); return string.IsNullOrEmpty(v) ? def : v; }
        void Awake() { baseUrl = EnvOr("VISTARA_OLLAMA_URL", baseUrl); model = EnvOr("VISTARA_LLM_MODEL", model); }

        public IEnumerator Probe()
        {
            using (var rq = UnityWebRequest.Get(baseUrl + "/api/tags"))
            {
                rq.timeout = 3;
                yield return rq.SendWebRequest();
                Online = rq.result == UnityWebRequest.Result.Success;
                ModelReady = Online && rq.downloadHandler.text.Contains("\"name\":\"" + model);
            }
            if (!Online && !triedStart)
            {
                triedStart = true;
                try { Process.Start(new ProcessStartInfo("ollama", "serve") { UseShellExecute = false, CreateNoWindow = true }); } catch (Exception) { }
            }
        }

        public IEnumerator Chat(List<OMsg> messages, Action<string> onReply, Action<string> onError)
        {
            var req = new OReq { model = model, messages = messages.ToArray(), stream = false, options = new OOpts() };
            byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(req));
            using (var rq = new UnityWebRequest(baseUrl + "/api/chat", "POST"))
            {
                rq.uploadHandler = new UploadHandlerRaw(body); rq.downloadHandler = new DownloadHandlerBuffer();
                rq.SetRequestHeader("Content-Type", "application/json"); rq.timeout = 120;
                yield return rq.SendWebRequest();
                if (rq.result != UnityWebRequest.Result.Success) { onError?.Invoke(rq.error); yield break; }
                var r = JsonUtility.FromJson<OResp>(rq.downloadHandler.text);
                if (r != null && r.message != null && !string.IsNullOrEmpty(r.message.content)) onReply?.Invoke(r.message.content.Trim());
                else onError?.Invoke(r != null && !string.IsNullOrEmpty(r.error) ? r.error : "empty reply");
            }
        }
    }

    /// <summary>
    /// STARA - the Vistara mascot: a shiny blue jelly slime. She stands beside the journey panel, wears a prop that matches the category you pick
    /// (chef hat for Kitchen, helmet for Bike, gloves for Boxing, dive mask for Scuba ...), and talks to you in a dialogue box right below the category panel.
    /// Her answers come from Meta Llama (Ollama). About the video she only uses facts from the analysis result.
    /// </summary>
    public class MascotAssistant : MonoBehaviour
    {
        public static MascotAssistant Instance { get; private set; }
        public float modelYawOffset = 180f;           // flip if the imported model faces away from you
        public float modelScale = 0.22f;
        const string Name = "Stara";

        class Outfit { public string[] props; public Color main, accent; public string wearing, line; }
        static Color C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
        static readonly Dictionary<string, Outfit> Outfits = new Dictionary<string, Outfit>
        {
            { "Kitchen",      new Outfit { props = new[] { "P_Chef" }, main = C("#f6f3ee"), accent = C("#ffffff"), wearing = "a chef hat", line = "Chef Stara reporting! Pick a kitchen video and I'll tell you what is cooking." } },
            { "Bike",         new Outfit { props = new[] { "P_Helmet" }, main = C("#e03c3c"), accent = C("#ffffff"), wearing = "a bike helmet", line = "Helmet on! Let's ride through some cycling footage." } },
            { "BikePolo",     new Outfit { props = new[] { "P_Helmet" }, main = C("#3b7bff"), accent = C("#ffffff"), wearing = "a bike-polo helmet", line = "Bike polo! Helmet on, mallet ready, let's find the best goals." } },
            { "Boxing",       new Outfit { props = new[] { "P_Gloves" }, main = C("#e03c3c"), accent = C("#ffffff"), wearing = "boxing gloves", line = "Gloves up! Pick a fight, I mean a video, and I'll find the best exchanges." } },
            { "Horse",        new Outfit { props = new[] { "P_Cowboy" }, main = C("#a8693a"), accent = C("#2a2630"), wearing = "a cowboy hat", line = "Howdy! Let's saddle up and watch some horse riding." } },
            { "Jumping",      new Outfit { props = new[] { "P_Headband" }, main = C("#34c759"), accent = C("#ffffff"), wearing = "a sport headband", line = "Ready to jump! Let's catch the best take-offs and landings." } },
            { "Longboarding", new Outfit { props = new[] { "P_CapFwd" }, main = C("#1fb5a8"), accent = C("#ffffff"), wearing = "a cap", line = "Cruising time! Pick a longboarding clip." } },
            { "Motor",        new Outfit { props = new[] { "P_Helmet", "P_Goggles" }, main = C("#2a2a33"), accent = C("#ff8a1f"), wearing = "a racing helmet with goggles", line = "Engines on! Let's find the fastest corners." } },
            { "Parkour",      new Outfit { props = new[] { "P_Headband" }, main = C("#ff8a1f"), accent = C("#ffffff"), wearing = "a headband", line = "Parkour! I'll try not to fall off the screen." } },
            { "Plane",        new Outfit { props = new[] { "P_AviatorCap", "P_Goggles" }, main = C("#8a5a34"), accent = C("#ffc247"), wearing = "an aviator cap and goggles", line = "Cleared for take-off! Pick a flight video." } },
            { "RockClimbing", new Outfit { props = new[] { "P_Headlamp" }, main = C("#ff7a1f"), accent = C("#ffffff"), wearing = "a climbing helmet with a headlamp", line = "Up we go! Let's find the hardest moves." } },
            { "Scuba",        new Outfit { props = new[] { "P_DiveMask" }, main = C("#17a6c8"), accent = C("#ffd23a"), wearing = "a dive mask and snorkel", line = "Blub blub! Dive mask on, let's explore underwater footage." } },
            { "Skate",        new Outfit { props = new[] { "P_CapBack" }, main = C("#e03c3c"), accent = C("#ffffff"), wearing = "a backwards cap", line = "Sk8 time! Let's find the best tricks." } },
            { "Ski",          new Outfit { props = new[] { "P_Beanie", "P_Goggles" }, main = C("#3b82f6"), accent = C("#7ad7ff"), wearing = "a beanie and ski goggles", line = "Brr! Beanie and goggles on, let's hit the slopes." } },
            { "Surfing",      new Outfit { props = new[] { "P_Sunglasses" }, main = C("#2a2630"), accent = C("#ffffff"), wearing = "cool sunglasses", line = "Surf's up! Shades on, pick a wave." } },
        };

        Transform model; OllamaClient llm; Canvas canvas; TextMeshProUGUI chatText, statusText; JourneyController journey;
        readonly Button[] askBtn = new Button[4]; readonly TextMeshProUGUI[] askTxt = new TextMeshProUGUI[4];
        readonly List<OMsg> history = new List<OMsg>(); readonly List<string> lines = new List<string>();
        readonly Dictionary<string, GameObject> propObjs = new Dictionary<string, GameObject>();
        readonly List<Material> jelly = new List<Material>();
        Transform actRoot; readonly Dictionary<string, GameObject> actProps = new Dictionary<string, GameObject>(); GameObject actProp; string actClip; Vector3 actBase; bool inspecting; float yawNow = 180f; float spin;
        string shownCategory; bool busy, placed, hadResult; float talkUntil, bobT, reveal; Vector3 basePos;

        // ---------------------------------------------------------------- boot: add Stara to the app scenes
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded += (s, m) => { if (IsAppScene(s.name)) Spawn(); };
            if (IsAppScene(SceneManager.GetActiveScene().name)) Spawn();
        }
        static bool IsAppScene(string n) { return n == "Workspace" || n == "Journey" || n == "MainMenu"; }
        public static void Spawn() { if (Instance == null) new GameObject("Stara").AddComponent<MascotAssistant>(); }

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; if (graph.IsValid()) graph.Destroy(); }

        void Start()
        {
            llm = gameObject.AddComponent<OllamaClient>();
            journey = FindFirstObjectByType<JourneyController>();
            BuildModel(); BuildPanel();
            StartCoroutine(Greet());
        }

        // ---------------------------------------------------------------- model: rigged jelly star + outfit + per-category props
        PlayableGraph graph; AnimationMixerPlayable mixer; readonly List<AnimationClipPlayable> players = new List<AnimationClipPlayable>();
        readonly List<string> clipNames = new List<string>(); readonly List<float> clipLen = new List<float>(); readonly List<float> weights = new List<float>(); string curAnim = "Idle";

        void BuildModel()
        {
            var prefab = Resources.Load<GameObject>("Mascot/VStar");
            if (prefab == null) { UnityEngine.Debug.LogWarning("[Vistara] Stara model not found in Resources/Mascot (VStar)"); return; }
            model = Instantiate(prefab, transform).transform; model.localScale = Vector3.one * modelScale;
            var jellySh = Shader.Find("Vistara/JellyStar"); var flatSh = Shader.Find("Vistara/MascotToon"); var faceSh = Shader.Find("Vistara/StaraFace");
            var shared = new Dictionary<string, Material>();
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                bool isProp = r.transform.name.StartsWith("P_");
                if (isProp) { propObjs[r.transform.name] = r.gameObject; }
                Bounds mb = new Bounds(Vector3.zero, Vector3.one * 0.1f);
                if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null) mb = smr.sharedMesh.bounds;
                else { var mf = r.GetComponent<MeshFilter>(); if (mf != null && mf.sharedMesh != null) mb = mf.sharedMesh.bounds; }
                var src = r.sharedMaterials; var dst = new Material[src.Length];
                for (int k = 0; k < src.Length; k++)
                {
                    var m = src[k]; string mname = m != null ? m.name : "none";
                    bool face = faceSh != null && (mname.Contains("Iris") || mname.Contains("Cheek"));
                    string key = (isProp ? r.transform.name + "/" : "") + mname + (face ? "#" + r.transform.name : "");
                    if (!shared.TryGetValue(key, out var nm))
                    {
                        Color c = Color.white;
                        if (m != null) { if (m.HasProperty("_BaseColor")) c = m.GetColor("_BaseColor"); else if (m.HasProperty("_Color")) c = m.GetColor("_Color"); }
                        if (mname.Contains("MAT_Star") && jellySh != null)
                        {
                            nm = new Material(jellySh) { name = key };
                            nm.SetColor("_TopColor", C("#fff3a6")); nm.SetColor("_BottomColor", C("#ffb000")); nm.SetColor("_RimColor", C("#fffbe0")); nm.SetColor("_InnerColor", C("#ffd04a"));
                            nm.SetFloat("_Opacity", 0.96f); nm.SetFloat("_GradMax", 1.05f); jelly.Add(nm);
                        }
                        else if (face)
                        {
                            nm = new Material(faceSh) { name = key };
                            nm.SetVector("_BMin", mb.min); nm.SetVector("_BSize", mb.size);
                            if (mname.Contains("Cheek")) { nm.SetFloat("_Mode", 2f); nm.SetColor("_Color", C("#ff8f86")); nm.SetFloat("_Alpha", 0.8f); }
                            else if (mname.Contains("IrisLt")) { nm.SetFloat("_Mode", 1f); nm.SetColor("_Color", C("#c9a4ff")); nm.SetColor("_Color2", C("#f2e8ff")); }
                            else { nm.SetFloat("_Mode", 1f); nm.SetColor("_Color", C("#3a1c86")); nm.SetColor("_Color2", C("#9360e8")); }
                        }
                        else { nm = new Material(flatSh) { name = key }; nm.SetColor("_Color", c); nm.SetFloat("_Shade", 1f); }
                        shared[key] = nm;
                    }
                    dst[k] = nm;
                }
                r.sharedMaterials = dst;
            }
            foreach (var pr in propObjs.Values) pr.SetActive(false);
            BuildAnimations(); BuildActivityProps();
        }

        /// <summary>Show the props that match the chosen category and tint them.</summary>
        void ApplyCategory(string cat)
        {
            foreach (var kv in propObjs) kv.Value.SetActive(false);
            if (inspecting && propObjs.TryGetValue("P_Magnifier", out var mg)) mg.SetActive(true);
            if (string.IsNullOrEmpty(cat) || !Outfits.TryGetValue(cat, out var o)) return;
            foreach (var pn in o.props)
            {
                if (!propObjs.TryGetValue(pn, out var go)) continue;
                go.SetActive(true);
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m.name.Contains("PropMain")) m.SetColor("_Color", o.main);
                        else if (m.name.Contains("PropAccent")) m.SetColor("_Color", o.accent);
                    }
            }
        }

        // ---------------------------------------------------------------- 3D activity props (pan, bike, surfboard ...) she acts with
        void BuildActivityProps()
        {
            var prefab = Resources.Load<GameObject>("Mascot/Activities"); if (prefab == null || model == null) return;
            var inst = Instantiate(prefab, model); inst.name = "Activities"; actRoot = inst.transform; actRoot.localPosition = Vector3.zero; actRoot.localRotation = Quaternion.identity; actRoot.localScale = Vector3.one;
            var flatSh = Shader.Find("Vistara/MascotToon"); var cache = new Dictionary<Material, Material>();
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                var src = r.sharedMaterials; var dst = new Material[src.Length];
                for (int k = 0; k < src.Length; k++)
                {
                    var m = src[k]; if (m == null) { dst[k] = null; continue; }
                    if (!cache.TryGetValue(m, out var nm))
                    {
                        Color c = Color.white; if (m.HasProperty("_BaseColor")) c = m.GetColor("_BaseColor"); else if (m.HasProperty("_Color")) c = m.GetColor("_Color");
                        if (flatSh != null) { nm = new Material(flatSh) { name = m.name }; nm.SetColor("_Color", c); nm.SetFloat("_Shade", 1f); } else nm = new Material(m);
                        cache[m] = nm;
                    }
                    dst[k] = nm;
                }
                r.sharedMaterials = dst;
            }
            foreach (Transform t in actRoot) { if (t.name.StartsWith("A_")) actProps[t.name] = t.gameObject; t.gameObject.SetActive(false); }
        }

        static readonly Dictionary<string, string> CatProp = new Dictionary<string, string> {
            { "Bike", "A_Bicycle" }, { "BikePolo", "A_Mallet" }, { "Boxing", "A_PunchBag" }, { "Horse", "A_HobbyHorse" }, { "Jumping", "A_Hurdle" }, { "Longboarding", "A_Longboard" },
            { "Motor", "A_Motorbike" }, { "Parkour", "A_ParkourBox" }, { "Plane", "A_PaperPlane" }, { "RockClimbing", "A_ClimbWall" }, { "Scuba", "A_Coral" }, { "Skate", "A_Skateboard" },
            { "Ski", "A_Skis" }, { "Surfing", "A_Surfboard" } };
        static readonly Dictionary<string, string> CatClip = new Dictionary<string, string> {
            { "Bike", "Ride" }, { "BikePolo", "Ride" }, { "Boxing", "Punch" }, { "Horse", "Ride" }, { "Jumping", "Reach" }, { "Longboarding", "Ride" }, { "Motor", "Ride" }, { "Parkour", "Climb" },
            { "Plane", "Fly" }, { "RockClimbing", "Climb" }, { "Scuba", "Dive" }, { "Skate", "Ride" }, { "Ski", "Ride" }, { "Surfing", "Ride" } };

        static bool Has(string s, params string[] keys) { foreach (var k in keys) if (s.StartsWith(k)) return true; return false; }
        static string VerbClip(string v)
        {
            v = (v ?? "").ToLowerInvariant();
            if (Has(v, "wash", "rinse", "dry", "clean", "wipe")) return "Wash";
            if (Has(v, "cut", "slice", "peel", "chop")) return "Chop";
            if (Has(v, "mix", "stir", "scoop", "spread", "knead")) return "Stir";
            if (Has(v, "pour", "empty", "shake", "drink", "fill")) return "Pour";
            if (Has(v, "open", "close", "turn", "press", "lift", "adjust")) return "Open";
            if (Has(v, "take", "put", "move", "get", "pick", "insert", "remove", "throw", "hold", "pull", "push", "grab")) return "Reach";
            return null;
        }
        static bool Any(string n, params string[] keys) { foreach (var k in keys) if (n.Contains(k)) return true; return false; }
        static string NounProp(string n)
        {
            n = (n ?? "").ToLowerInvariant();
            if (Any(n, "pan", "wok")) return "A_Pan";
            if (Any(n, "pot", "kettle", "lid")) return "A_Pot";
            if (Any(n, "plate", "dish")) return "A_Plate";
            if (Any(n, "bowl")) return "A_Bowl";
            if (Any(n, "cup", "glass", "mug")) return "A_Cup";
            if (Any(n, "bottle", "jar", "oil", "sauce")) return "A_Bottle";
            if (Any(n, "knife")) return "A_Knife";
            if (Any(n, "spoon", "spatula", "fork", "ladle")) return "A_Spoon";
            if (Any(n, "board", "onion", "pepper", "potato", "tomato", "carrot")) return "A_Board";
            if (Any(n, "box", "bag", "container", "drawer", "cupboard", "fridge")) return "A_Box";
            return null;
        }

        /// <summary>She acts out the recognised action: the matching animation plus a 3D prop for the object (or for the sport of the category).</summary>
        public void ActAs(string verb, string noun)
        {
            if (verb == null) { StopActing(); return; }
            string cat = AppState.Category; string clip = VerbClip(verb), prop = NounProp(noun);
            if (cat != null && CatProp.ContainsKey(cat)) { prop = CatProp[cat]; if (clip == null || cat == "Boxing" || cat == "Plane" || cat == "Scuba") clip = CatClip[cat]; }
            if (clip == null) clip = "Reach";
            actClip = clip; SetActProp(prop); PlayAnim(clip);
        }
        /// <summary>Which 3D prop goes with a recognised action (sport prop for the category, else the object).</summary>
        public static string PropFor(string cat, string noun) { string prop = NounProp(noun); if (cat != null && CatProp.ContainsKey(cat)) prop = CatProp[cat]; return prop; }
        public void StopActing() { if (actClip == null && actProp == null) return; actClip = null; SetActProp(null); PlayAnim(inspecting ? "Inspect" : "Idle"); }

        void SetActProp(string name)
        {
            if (actProp != null) actProp.SetActive(false);
            actProp = null;
            if (name != null && actProps.TryGetValue(name, out var go))
            {
                actProp = go; go.SetActive(true); go.transform.localPosition = new Vector3(-0.85f, 0f, 0.15f); go.transform.localRotation = Quaternion.Euler(0, -20f, 0); actBase = go.transform.localPosition;
            }
        }

        void TickProp()
        {
            if (actProp == null) return;
            spin += Time.deltaTime * 260f;
            foreach (Transform c in actProp.transform) if (c.name.Contains("Wheel")) c.localRotation = Quaternion.AngleAxis(spin, Vector3.right);
            bool air = actProp.name == "A_PaperPlane";
            actProp.transform.localPosition = actBase + new Vector3(0, (air ? 0.35f : 0f) + Mathf.Sin(Time.time * 2f) * (air ? 0.06f : 0.012f), 0);
        }

        /// <summary>While the engine analyses the video she stands beside it and looks at it through a magnifying glass.</summary>
        void SetInspect(bool on)
        {
            if (on == inspecting) return;
            inspecting = on;
            if (propObjs.TryGetValue("P_Magnifier", out var mag)) mag.SetActive(on);
            if (on) PlayAnim("Inspect"); else if (actClip == null) PlayAnim("Idle");
        }

        static readonly HashSet<string> Loops = new HashSet<string> { "Idle", "Talk", "Inspect", "Punch", "Stir", "Chop", "Pour", "Wash", "Reach", "Open", "Ride", "Climb", "Fly", "Dive" };
        string RestAnim() { return actClip ?? (inspecting ? "Inspect" : "Idle"); }

        void BuildAnimations()
        {
            var clips = Resources.LoadAll<AnimationClip>("Mascot/VStar").Where(c => c.name.Contains("AN_VStar_")).ToList();
            if (clips.Count == 0) return;
            var animator = model.GetComponentInChildren<Animator>(); if (animator == null) animator = model.gameObject.AddComponent<Animator>();
            graph = PlayableGraph.Create("Stara"); graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            mixer = AnimationMixerPlayable.Create(graph, clips.Count);
            for (int i = 0; i < clips.Count; i++)
            {
                var cp = AnimationClipPlayable.Create(graph, clips[i]); graph.Connect(cp, 0, mixer, i);
                string n = clips[i].name.Substring(clips[i].name.LastIndexOf('_') + 1);
                players.Add(cp); clipNames.Add(n); clipLen.Add(clips[i].length); weights.Add(n == "Idle" ? 1f : 0f); mixer.SetInputWeight(i, weights[i]);
            }
            var output = AnimationPlayableOutput.Create(graph, "Stara", animator); output.SetSourcePlayable(mixer);
            graph.Play();
        }

        public void PlayAnim(string n) { int i = clipNames.IndexOf(n); if (i < 0) return; curAnim = n; players[i].SetTime(0); }

        void TickAnimations()
        {
            if (players.Count == 0) return;
            for (int i = 0; i < players.Count; i++)
            {
                float target = clipNames[i] == curAnim ? 1f : 0f;
                weights[i] = Mathf.MoveTowards(weights[i], target, Time.deltaTime * 6f); mixer.SetInputWeight(i, weights[i]);
                if (players[i].GetTime() >= clipLen[i] - 0.02f)
                {
                    if (Loops.Contains(clipNames[i])) players[i].SetTime(0);
                    else if (clipNames[i] == curAnim) PlayAnim(RestAnim());
                }
            }
            bool talking = busy || Time.time < talkUntil;
            if (talking && curAnim == "Idle") PlayAnim("Talk"); else if (!talking && curAnim == "Talk") PlayAnim(RestAnim());
            foreach (var m in jelly) m.SetFloat("_Wobble", Mathf.Lerp(m.GetFloat("_Wobble"), talking ? 0.8f : 0.35f, Time.deltaTime * 4f));
        }

        // ---------------------------------------------------------------- placement: under the category panel, or under the summary viewer once it opens
        const float PanelPx = 1700f;
        void FollowFrame(Transform frame, float wM, float hM, float side = -1f, float turn = 0f)
        {
            float sc = wM / PanelPx;                                                   // the dialogue box is as wide as the panel above it
            transform.SetPositionAndRotation(frame.TransformPoint(new Vector3(side * (wM * 0.5f + 0.085f), -hM * 0.5f + 0.05f, -0.015f)), frame.rotation);
            canvas.transform.SetPositionAndRotation(frame.TransformPoint(new Vector3(0, -hM * 0.5f - 0.012f - (300 * sc) * 0.5f, -0.002f)), frame.rotation);
            canvas.transform.localScale = Vector3.one * (sc * frame.lossyScale.x);
            if (model != null) { yawNow = Mathf.LerpAngle(yawNow, modelYawOffset + turn, 1f - Mathf.Exp(-5f * Time.deltaTime)); model.localRotation = Quaternion.Euler(0, yawNow, 0); }
        }

        void Update()
        {
            bobT += Time.deltaTime; TickAnimations();
            var cam = Camera.main; if (cam == null) return;
            bool hudOn = journey != null && journey.Hud != null && journey.Hud.Visible;
            SetInspect(journey != null && journey.Running && !hudOn);
            TickProp();
            if (hudOn) FollowFrame(journey.Hud.Root, 1040f * 0.00026f, 640f * 0.00026f, +1f, 40f);          // right of the video, turned a little towards it (never her back)
            else if (journey != null && journey.Stage != null) FollowFrame(journey.transform, JourneyController.SW * 0.00022f, JourneyController.SH * 0.00022f, -1f, inspecting ? -40f : 0f);
            else if (MainMenuController.Instance != null) FollowFrame(MainMenuController.Instance.transform, MainMenuController.W * 0.00022f, MainMenuController.H * 0.00022f, -1f, 0f);          // start menu: she stands on the panel's left edge, exactly as in the journey
            else           // no journey panel (workspace scene): stand near the user and billboard the dialogue
            {
                if (!placed) { Vector3 f = cam.transform.forward; f.y = 0; if (f.sqrMagnitude < 0.01f) f = Vector3.forward; f.Normalize(); basePos = cam.transform.position + f * 0.85f + Vector3.Cross(Vector3.up, f) * 0.55f + Vector3.down * 0.62f; transform.position = basePos; placed = true; }
                Vector3 toCam = cam.transform.position - transform.position; toCam.y = 0;
                if (toCam.sqrMagnitude > 0.01f && model != null) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toCam.normalized, Vector3.up) * Quaternion.Euler(0, modelYawOffset, 0), 1f - Mathf.Exp(-4f * Time.deltaTime));
                if (canvas != null) { canvas.transform.position = basePos + Vector3.up * 0.45f; canvas.transform.rotation = Quaternion.LookRotation(canvas.transform.position - cam.transform.position, Vector3.up); canvas.transform.localScale = Vector3.one * 0.00042f; }
            }
            bool talking = busy || Time.time < talkUntil;
            if (model != null) model.localPosition = new Vector3(0, talking ? Mathf.Abs(Mathf.Sin(bobT * 9f)) * 0.01f : Mathf.Sin(bobT * 1.6f) * 0.003f, 0);

            string cat = AppState.Category;                       // a new category was chosen in the carousel
            if (cat != shownCategory && !string.IsNullOrEmpty(cat)) { shownCategory = cat; OnCategoryChosen(cat); }
            if (AppState.HasResult != hadResult) { hadResult = AppState.HasResult; RelabelAsks(); if (hadResult) { PlayAnim("Happy"); StartCoroutine(NarrateRoutine()); } }
            Observe();
            if (chatText != null) { reveal += Time.deltaTime * 55f; chatText.maxVisibleCharacters = Mathf.FloorToInt(reveal); }
        }

        void OnCategoryChosen(string cat)
        {
            ApplyCategory(cat); PlayAnim("Happy");
            lines.Clear(); history.Clear();
            Say(Name, Outfits.TryGetValue(cat, out var o) ? o.line : "Nice choice! Let's see what's in " + cat + ".");
            RelabelAsks();
        }

        // ---------------------------------------------------------------- dialogue box (under the category panel)
        void BuildPanel()
        {
            canvas = UIKit.WorldCanvas("StaraDialogue", 1700, 300, null, 0.00022f);
            var rt = (RectTransform)canvas.transform;
            var back = UIKit.Box(rt, "Back", new Color(0.04f, 0.06f, 0.15f, 0.90f), 30); UIKit.Fill(back.rectTransform);
            UIKit.Outline(back.transform, Theme.Orange * new Color(1, 1, 1, 0.75f), 30, 3f);
            UIKit.TextAt(rt, "STARA", 36, 34, 14, 260, 50, Theme.Orange, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            statusText = UIKit.TextAt(rt, "", 22, 300, 14, 420, 50, Theme.TextDim, TextAlignmentOptions.MidlineLeft);
            chatText = UIKit.TextAt(rt, "", 32, 34, 68, 1100, 210, Theme.Text, TextAlignmentOptions.TopLeft);
            chatText.overflowMode = TextOverflowModes.Truncate; chatText.textWrappingMode = TextWrappingModes.Normal;
            for (int i = 0; i < 2; i++)
            {
                int k = i;
                askBtn[i] = UIKit.Button(rt, "", 1180 + i * 262, 100, 250, 120, () => AskKind(k), Theme.Card, Theme.Text, null, 24, 18);
                askTxt[i] = null;
            }
            RelabelAsks();
        }

        static readonly string[] PreLabels = { "About this", "Pick for me" };
        static readonly string[] PostLabels = { "Summarise", "Show me" };
        void RelabelAsks()
        {
            var labels = AppState.HasResult ? PostLabels : PreLabels;
            if (AppState.HasResult && demoRunning) { labels = (string[])PostLabels.Clone(); labels[1] = "Stop"; }
            for (int i = 0; i < 2; i++)
            {
                if (askBtn[i] == null) continue;
                var t = askBtn[i].GetComponentInChildren<TextMeshProUGUI>();
                if (t == null) t = UIKit.TextAt(askBtn[i].transform, "", 26, 0, 0, 250, 112, Theme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
                t.text = labels[i]; t.enableAutoSizing = true; t.fontSizeMin = 16; t.fontSizeMax = 26;
            }
        }

        string CatName() { return string.IsNullOrEmpty(AppState.Category) ? "video" : AppState.Category; }
        void AskKind(int k)
        {
            if (AppState.HasResult)
            {
                if (k == 1) { ToggleDemo(); return; }
                Ask("Give me a short summary of this video.");
            }
            else
            {
                Ask(k == 0 ? "Tell me about the " + CatName() + " category and what I can do here." : "Which " + CatName() + " video should I pick? Give me a fun suggestion.");
            }
        }

        /// <summary>Stara says a line in her dialogue box (used by the guide girl to chat with her).</summary>
        public void Chat(string text) { if (chatText != null) Say(Name, text); }

        void Say(string who, string text)
        {
            lines.Add("<color=#" + (who == "You" ? "19E3FF" : "FFB000") + "><b>" + who + "</b></color>  " + text);
            while (lines.Count > 2) lines.RemoveAt(0);
            chatText.text = string.Join("\n", lines);
            reveal = who == "You" ? 9999f : Mathf.Max(0f, chatText.text.Length - text.Length - 40);
            if (who != "You") { talkUntil = Time.time + Mathf.Clamp(text.Length * 0.04f, 1.5f, 6f); lastSpoke = Time.time; }
        }

        IEnumerator Greet()
        {
            yield return llm.Probe();
            statusText.text = llm.Online ? (llm.ModelReady ? "Llama online" : "model not pulled") : "starting Llama...";
            statusText.color = llm.Online && llm.ModelReady ? Theme.Green : Theme.Orange;
            PlayAnim("Wave");
            Say(Name, "Hi, I'm Stara! Choose a category and I'll dress up for it. I'll be watching what you do.");
            if (!llm.Online) { yield return new WaitForSeconds(6f); yield return llm.Probe(); statusText.text = llm.Online ? "Llama online" : "Llama offline"; }
        }

        public void Ask(string question) { if (!busy) StartCoroutine(AskRoutine(question)); }

        IEnumerator AskRoutine(string question)
        {
            busy = true; Say("You", question);
            history.Add(new OMsg { role = "user", content = question });
            while (history.Count > 6) history.RemoveAt(0);
            var msgs = new List<OMsg> { new OMsg { role = "system", content = SystemPrompt() } }; msgs.AddRange(history);
            string reply = null, err = null; statusText.text = "thinking...";
            yield return llm.Chat(msgs, r => reply = r, e => err = e);
            if (reply != null) { PlayAnim("Happy"); history.Add(new OMsg { role = "assistant", content = reply }); Say(Name, reply); statusText.text = "Llama online"; }
            else { Say(Name, AppState.HasResult ? SummaryText() : "I can't chat freely right now (Ollama is offline), but pick a video and I will still analyse it for you."); statusText.text = "Llama offline"; }
            busy = false;
        }

        // ---------------------------------------------------------------- awareness: she watches what you do and speaks her mind
        VideoController vc; string lastVideo, lastPhase; bool lastRunning, lastHud, lastPlaying, reacting, demoRunning, narrating; float lastVideoT = -1f, lastSpoke, lastEventTime, gazeTime, lastPoll;

        void Observe()
        {
            if (busy || reacting || demoRunning || narrating) return;
            var cam = Camera.main; if (cam == null) return;
            if (vc == null) vc = journey != null ? journey.Source : FindFirstObjectByType<VideoController>();
            string ev = null;
            if (AppState.VideoPath != lastVideo && !string.IsNullOrEmpty(AppState.VideoPath)) { lastVideo = AppState.VideoPath; ev = "The user just selected the video '" + System.IO.Path.GetFileNameWithoutExtension(AppState.VideoPath) + "'."; }
            else if (journey != null && journey.Running != lastRunning) { lastRunning = journey.Running; if (lastRunning) ev = "The user started the analysis; the AI engine is now reading the video."; }
            else if (journey != null && journey.Hud != null && journey.Hud.Visible != lastHud) { lastHud = journey.Hud.Visible; ev = lastHud ? "The summary viewer just opened, showing the whole video with its key moments." : "The user closed the summary viewer."; }
            else if (journey != null && journey.Hud != null && journey.Hud.Visible && journey.Hud.PhaseName != lastPhase) { lastPhase = journey.Hud.PhaseName; ev = lastPhase == "HighlightReel" ? "The user switched to the highlight reel (only the key moments play)." : "The user switched back to the full video."; }
            if (ev == null && vc != null && vc.Prepared)
            {
                bool playing = vc.IsPlaying; float t = vc.TimeSec; bool reel = journey != null && journey.Hud != null && journey.Hud.Visible && journey.Hud.PhaseName == "HighlightReel";
                if (lastVideoT >= 0f && !reel && Mathf.Abs(t - lastVideoT) > 3f && Time.time - lastPoll < 0.5f) ev = "The user jumped in the video to " + T(t) + ".";
                else if (lastVideoT >= 0f && playing != lastPlaying) ev = playing ? "The user resumed the video at " + T(t) + "." : "The user paused the video at " + T(t) + ".";
                lastPlaying = playing; lastVideoT = t; lastPoll = Time.time;
            }
            Vector3 to = (transform.position - cam.transform.position).normalized;                 // is the user looking at her?
            gazeTime = Vector3.Angle(cam.transform.forward, to) < 12f ? gazeTime + Time.deltaTime : 0f;
            if (ev == null && gazeTime > 2.5f && Time.time - lastSpoke > 25f) { ev = "The user is looking right at you."; gazeTime = 0f; }
            if (ev == null && Time.time - Mathf.Max(lastSpoke, lastEventTime) > 40f) ev = "The user has been quiet for a while.";
            if (ev != null) React(ev);
        }

        void React(string ev)
        {
            if (Time.time - lastSpoke < 6f && !ev.Contains("selected") && !ev.Contains("analysis")) return;       // do not chatter
            lastEventTime = Time.time; StartCoroutine(ReactRoutine(ev));
        }

        static string FallbackReaction(string ev)
        {
            if (ev.Contains("selected")) return "Ooh, nice pick! Press ANALYSE when you are ready.";
            if (ev.Contains("started the analysis")) return "Hang tight, I'm watching the progress bar!";
            if (ev.Contains("opened")) return "Look at all those key moments! Want me to walk you through them?";
            if (ev.Contains("closed")) return "Back to the menu. Pick another video?";
            if (ev.Contains("highlight reel")) return "Just the best bits, yum!";
            if (ev.Contains("full video")) return "Back to the whole story.";
            if (ev.Contains("jumped")) return "Whoa, skipping ahead!";
            if (ev.Contains("resumed")) return "And we're rolling!";
            if (ev.Contains("paused")) return "Taking a closer look?";
            if (ev.Contains("looking right at you")) return "Hi there! Need a hand?";
            return "Psst, want me to show you the highlights?";
        }

        IEnumerator ReactRoutine(string ev)
        {
            reacting = true;
            var msgs = new List<OMsg> { new OMsg { role = "system", content = SystemPrompt() + "You are watching over the user's shoulder. React to the EVENT as a playful friend in ONE short sentence (max 16 words). Do not repeat what you said before.\n" },
                                        new OMsg { role = "user", content = "EVENT: " + ev } };
            string reply = null;
            if (llm != null && llm.Online) yield return llm.Chat(msgs, r => reply = r, e => { });
            if (string.IsNullOrEmpty(reply)) reply = FallbackReaction(ev);
            Say(Name, reply); lastSpoke = Time.time; PlayAnim(ev.Contains("jumped") || ev.Contains("looking") ? "Wave" : "Happy");
            reacting = false;
        }

        // ---------------------------------------------------------------- the analysis, in words (built from the engine's result - no LLM needed), then the highlight tour with Vee re-enacting each moment
        static string TopList(IEnumerable<string> items, int n) { return string.Join(", ", items.Where(x => !string.IsNullOrEmpty(x)).Take(n)); }

        string SummaryText()
        {
            if (!AppState.HasResult) return "No video has been analysed yet.";
            var o = AppState.Result.output; var acts = o.actions ?? new ActionItem[0]; var hl = (o.highlights ?? new SegmentItem[0]).Select(h => h.segment).OrderBy(x => x[0]).ToList();
            float kept = hl.Sum(h => h[1] - h[0]); string label = o.label != null ? o.label.name : "first-person";
            var verbs = acts.GroupBy(x => Clean(x.verb)).OrderByDescending(g => g.Sum(x => x.End - x.Start)).Select(g => g.Key);
            var nouns = acts.GroupBy(x => Clean(x.noun)).OrderByDescending(g => g.Sum(x => x.End - x.Start)).Select(g => g.Key);
            return "This is a " + label + " video, " + T(o.duration_sec) + " long. I recognised " + acts.Length + " actions, mostly " + TopList(verbs, 3) + " with " + TopList(nouns, 3) + ". The summary keeps " + hl.Count + " key moments, " + Mathf.RoundToInt(kept / Mathf.Max(1f, o.duration_sec) * 100f) + "% of the video.";
        }

        IEnumerator NarrateRoutine()
        {
            narrating = true;
            var o = AppState.Result.output; var acts = o.actions ?? new ActionItem[0]; var hl = (o.highlights ?? new SegmentItem[0]).Select(h => h.segment).OrderBy(x => x[0]).ToList();
            string model = BackendClient.Instance != null && !string.IsNullOrEmpty(BackendClient.Instance.Recognizer) ? BackendClient.Instance.Recognizer : "the AI model";
            string label = o.label != null ? o.label.name : "first-person";
            Say(Name, "Analysis done! " + model + " watched " + T(o.duration_sec) + " of " + label + " footage."); yield return new WaitForSeconds(4.5f);
            if (!AppState.HasResult) { narrating = false; yield break; }
            var verbs = acts.GroupBy(x => Clean(x.verb)).OrderByDescending(g => g.Sum(x => x.End - x.Start)).Select(g => g.Key);
            var nouns = acts.GroupBy(x => Clean(x.noun)).OrderByDescending(g => g.Sum(x => x.End - x.Start)).Select(g => g.Key);
            Say(Name, "I recognised " + acts.Length + " actions: mostly " + TopList(verbs, 3) + ", with " + TopList(nouns, 3) + "."); yield return new WaitForSeconds(5f);
            float kept = hl.Sum(h => h[1] - h[0]);
            Say(Name, "I kept " + hl.Count + " key moments, " + Mathf.RoundToInt(kept / Mathf.Max(1f, o.duration_sec) * 100f) + "% of the video."); yield return new WaitForSeconds(4f);
            narrating = false;
            if (AppState.HasResult && hl.Count > 0 && !demoRunning) { Say(Name, "Come closer! Vee acts out each one while I tell you what is happening."); yield return new WaitForSeconds(3.5f); if (AppState.HasResult && !demoRunning) StartCoroutine(DemoRoutine()); }
        }

        // ---------------------------------------------------------------- she demonstrates the key highlights: seeks the video to each one and tells you what happens
        public void ToggleDemo() { if (demoRunning) { demoRunning = false; Say(Name, "Okay, I'll stop here."); RelabelAsks(); } else StartCoroutine(DemoRoutine()); }

        IEnumerator DemoRoutine()
        {
            if (!AppState.HasResult) { Say(Name, "Analyse a video first, then I can show you its highlights."); yield break; }
            if (vc == null) vc = journey != null ? journey.Source : FindFirstObjectByType<VideoController>();
            if (vc == null || !vc.Prepared) { Say(Name, "I can't find the video player right now."); yield break; }
            var o = AppState.Result.output; var hl = (o.highlights ?? new SegmentItem[0]).Select(h => h.segment).OrderBy(x => x[0]).ToList();
            if (hl.Count == 0) { Say(Name, "I did not find any key moments in this video."); yield break; }
            demoRunning = true; RelabelAsks();
            Say(Name, "Let me show you the " + hl.Count + " key moments!"); PlayAnim("Wave"); yield return new WaitForSeconds(2.2f);
            string[] openers = { "First up", "Next", "Then", "Look at this", "And now", "Don't miss this", "Almost there", "Last one" };
            for (int i = 0; i < hl.Count && demoRunning; i++)
            {
                var seg = hl[i];
                var best = (o.actions ?? new ActionItem[0]).Where(x => x.End > seg[0] && x.Start < seg[1]).OrderByDescending(x => x.score).FirstOrDefault();
                string what = best != null ? Clean(best.verb) + " " + Clean(best.noun) : "an important moment";
                string op = i == hl.Count - 1 && hl.Count > 1 ? "Last one" : openers[Mathf.Min(i, openers.Length - 2)];
                Say(Name, op + " at " + T(seg[0]) + ": " + what + (best != null ? " (my best guess)." : "."));
                PlayAnim("Happy"); lastSpoke = Time.time;
                vc.Seek(seg[0]); vc.Play();
                float t0 = Time.time, dur = Mathf.Max(3f, seg[1] - seg[0]);
                while (demoRunning && vc.Prepared && vc.TimeSec < seg[1] - 0.1f && Time.time - t0 < dur + 4f) yield return null;
            }
            if (demoRunning) { vc.Pause(); Say(Name, "That's the summary! Ask me anything about it."); PlayAnim("Jump"); lastSpoke = Time.time; }
            demoRunning = false; RelabelAsks();
        }

        // ---------------------------------------------------------------- what she may talk about
        string SystemPrompt()
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are Stara, a cute, cheerful blue jelly slime mascot living inside a VR app that summarises first-person videos. Stay in character.");
            sb.AppendLine("Reply in short, warm sentences (at most 3). Never use emojis.");
            string cat = AppState.Category;
            if (!string.IsNullOrEmpty(cat) && Outfits.TryGetValue(cat, out var o)) sb.AppendLine("The user picked the category '" + cat + "'; you are wearing " + o.wearing + " to match.");
            if (AppState.HasResult)
            {
                sb.AppendLine("Use ONLY the VIDEO FACTS below when you talk about the video. If something is not in the facts, say you cannot tell from the analysis.");
                sb.AppendLine("Action names are guesses made by a recognition model and can be wrong; say so when asked about a specific action. Never invent timestamps.");
                sb.AppendLine("When asked for a summary or the key moments, name the category, then list up to 4 key moments as 'mm:ss action' taken from the facts.");
                sb.AppendLine("VIDEO FACTS:"); sb.Append(VideoFacts());
            }
            else sb.AppendLine("No video has been analysed yet: help the user choose a video, explain the category, or just chat. Do not claim to know what is in any video.");
            return sb.ToString();
        }

        static string T(float s) { return AppState.FormatTime(s); }
        static string Clean(string s) { return s.Replace('-', ' ').Replace(':', ' '); }

        string VideoFacts()
        {
            if (!AppState.HasResult) return "No video has been analysed yet.\n";
            var o = AppState.Result.output; var a = AppState.Result.analysis; var sb = new StringBuilder();
            float dur = o.duration_sec;
            sb.AppendLine("Category: " + (o.label != null ? o.label.name : "unknown") + "; length " + T(dur) + ".");
            var hl = (o.highlights ?? new SegmentItem[0]).Select(h => h.segment).OrderBy(s => s[0]).ToList();
            float kept = hl.Sum(h => h[1] - h[0]);
            sb.AppendLine("The summary keeps " + hl.Count + " key moments, " + T(kept) + " in total (" + Mathf.RoundToInt(kept / Mathf.Max(1f, dur) * 100f) + "% of the video).");
            int n = 0;
            foreach (var h in hl)
            {
                if (++n > 8) break;
                var best = (o.actions ?? new ActionItem[0]).Where(x => x.End > h[0] && x.Start < h[1]).OrderByDescending(x => x.score).FirstOrDefault();
                sb.AppendLine("- Key moment " + n + ": " + T(h[0]) + " to " + T(h[1]) + (best != null ? ", likely '" + Clean(best.verb) + " " + Clean(best.noun) + "'" : ""));
            }
            if (a != null && a.objects != null && a.objects.Length > 0)
                sb.AppendLine("Objects seen most: " + string.Join(", ", a.objects.Take(6).Select(x => Clean(x.noun) + " (" + Mathf.RoundToInt(x.seconds) + "s)")) + ".");
            if (a != null && a.verbs != null && a.verbs.Length > 0)
                sb.AppendLine("Most common activities: " + string.Join(", ", a.verbs.Take(5).Select(x => Clean(x.verb))) + ".");
            var vc = FindFirstObjectByType<VideoController>();
            if (vc != null && vc.Prepared)
            {
                float t = vc.TimeSec;
                var cur = (o.actions ?? new ActionItem[0]).Where(x => x.Start <= t && t <= x.End).OrderByDescending(x => x.score).FirstOrDefault();
                sb.AppendLine("Now playing at " + T(t) + (cur != null ? ": likely '" + Clean(cur.verb) + " " + Clean(cur.noun) + "'." : ": no action recognised here."));
            }
            return sb.ToString();
        }
    }
}
