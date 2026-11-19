using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vistara
{
    /// <summary>
    /// "Vee", the guide. Shown in the menu in her default outfit, and changes into a category outfit as soon as a category is chosen.
    /// Procedural animation on the imported rig (idle, blink, talk gestures with text-driven lip-sync, walking, re-enacting the analysed action), a speech bubble, and a chat with Stara.
    /// </summary>
    public class GuideGirl : MonoBehaviour
    {
        public static GuideGirl Instance { get; private set; }
        public float modelScale = 0.7f, floorY = 0f;

        class Look { public string tee, sleeve, line; }
        static readonly Dictionary<string, Look> Looks = new Dictionary<string, Look>
        {
            { "Kitchen",      new Look { tee = "#f6f3ee", sleeve = "#4fc3b0", line = "All set. Let's cook!" } },
            { "Bike",         new Look { tee = "#2d7ff9", sleeve = "#f4f1ea", line = "Let's go for a ride!" } },
            { "BikePolo",     new Look { tee = "#e5384f", sleeve = "#f4f1ea", line = "Let's play bike polo!" } },
            { "Boxing",       new Look { tee = "#d92b2b", sleeve = "#3a1218", line = "Gloves up. Let's box!" } },
            { "Horse",        new Look { tee = "#f4f1ea", sleeve = "#7a4a27", line = "Giddy up! Let's ride!" } },
            { "Jumping",      new Look { tee = "#ff5fa2", sleeve = "#31d6e8", line = "Let's jump!" } },
            { "Longboarding", new Look { tee = "#1fc7b6", sleeve = "#f4f1ea", line = "Let's go for a cruise!" } },
            { "Motor",        new Look { tee = "#1c1d24", sleeve = "#ff7a1a", line = "Engines on. Let's go!" } },
            { "Parkour",      new Look { tee = "#ff8a1f", sleeve = "#22232b", line = "Let's run and climb!" } },
            { "Plane",        new Look { tee = "#8a5a34", sleeve = "#e8e2d0", line = "Ready for take-off!" } },
            { "RockClimbing", new Look { tee = "#ffd024", sleeve = "#2a2d38", line = "Let's climb!" } },
            { "Scuba",        new Look { tee = "#1fb7cf", sleeve = "#20242e", line = "Let's go diving!" } },
            { "Skate",        new Look { tee = "#c8ff3a", sleeve = "#22232b", line = "Let's skate!" } },
            { "Ski",          new Look { tee = "#e5384f", sleeve = "#f4f1ea", line = "Let's hit the slopes!" } },
            { "Surfing",      new Look { tee = "#17c1c9", sleeve = "#ff8f7a", line = "Surf's up. Let's go!" } },
        };
        static readonly string[] HeadCovers = { "Helmet", "Hat", "Cap", "Beanie", "Headgear" };

        Transform model, rootBone; readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        readonly Dictionary<Transform, Quaternion> rest = new Dictionary<Transform, Quaternion>();
        readonly List<SkinnedMeshRenderer> smrs = new List<SkinnedMeshRenderer>();
        readonly Dictionary<string, Material> flat = new Dictionary<string, Material>();
        readonly List<GameObject> outfitPieces = new List<GameObject>(); Material teeMat; GameObject outfitRoot; GameObject starPin; string shownCat; Shader toonSh, flatSh;
        Transform shinL, shinR, footL, footR; Transform armUL, armUR, armFL, armFR, handL, handR, thighL, thighR, head, spine, spine2, tailL, tailR;
        Canvas bubble; TextMeshProUGUI bubbleText; string speech = ""; float speechStart, speechEnd, blinkAt, blinkT = -1f, walkPh; Vector3 targetPos; bool placed;
        string gesture = "Idle"; string gestureLabel; float gestureT; bool hadResult; float nextIdleChat;
        JourneyController journey; Reenactor reen; bool closeUp;
        Quaternion yaw = Quaternion.identity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded += (s, m) => { if (s.name == "Workspace" || s.name == "Journey") Spawn(); };
            var n = SceneManager.GetActiveScene().name; if (n == "Workspace" || n == "Journey") Spawn();
        }
        static void Spawn() { if (Instance == null) new GameObject("VeeGuide").AddComponent<GuideGirl>(); }
        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        static Color C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
        Transform B(string n) { bones.TryGetValue(n, out var t); return t; }

        // ---------------------------------------------------------------- build
        void Start()
        {
            journey = FindFirstObjectByType<JourneyController>();
            var prefab = Resources.Load<GameObject>("Girl/Vistara_Girl");
            if (prefab == null) { Debug.LogWarning("[Vistara] Girl model missing (Resources/Girl/Vistara_Girl)"); enabled = false; return; }
            model = Instantiate(prefab, transform).transform; model.localScale = Vector3.one * modelScale; model.localPosition = Vector3.zero;
            toonSh = Shader.Find("Vistara/GirlToon"); flatSh = Shader.Find("Vistara/MascotToon");
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) if (!bones.ContainsKey(t.name)) bones[t.name] = t;
            foreach (var a in model.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            armUL = B("upper_arm.L"); armUR = B("upper_arm.R"); armFL = B("forearm.L"); armFR = B("forearm.R"); handL = B("hand.L"); handR = B("hand.R");
            thighL = B("thigh.L"); thighR = B("thigh.R"); shinL = B("shin.L"); shinR = B("shin.R"); footL = B("foot.L"); footR = B("foot.R"); head = B("Head"); spine = B("spine.001"); spine2 = B("spine.003"); tailL = B("Tail.L"); tailR = B("Tail.R");
            foreach (var b in new[] { armUL, armUR, armFL, armFR, thighL, thighR, shinL, shinR, footL, footR, head, spine, spine2, tailL, tailR }) if (b != null) rest[b] = b.localRotation;
            foreach (var n in bones.Keys.Where(k => k.StartsWith("Tail.") || k.StartsWith("Hair ")).ToList()) rest[bones[n]] = bones[n].localRotation;
            BuildMaterials();
            BuildBubble(); reen = gameObject.AddComponent<Reenactor>();
            blinkAt = Time.time + 2f; nextIdleChat = Time.time + 1.5f; rescanAt = Time.time + 0.6f;
            if (!bones.ContainsKey("upper_arm.L")) Debug.LogWarning("[Vistara] girl rig bones not found: " + string.Join(",", bones.Keys.Take(12)));
        }

        Material Flat(Color c, float shade = 0.84f)
        {
            string k = ColorUtility.ToHtmlStringRGB(c) + shade; if (flat.TryGetValue(k, out var m)) return m;
            m = new Material(flatSh != null ? flatSh : Shader.Find("Universal Render Pipeline/Unlit")); m.SetColor("_Color", c); if (m.HasProperty("_Shade")) m.SetFloat("_Shade", shade); flat[k] = m; return m;
        }
        static Color ColorOf(string matName)
        {
            var m = System.Text.RegularExpressions.Regex.Match(matName ?? "", "M_([0-9a-fA-F]{6})");
            return m.Success && ColorUtility.TryParseHtmlString("#" + m.Groups[1].Value, out var c) ? c : new Color(0.75f, 0.75f, 0.78f);
        }

        void BuildMaterials()
        {
            var texOf = new Dictionary<string, string> { { "Body", "Body_Blue" }, { "Clothes", "Clothes" }, { "Clothes_Tee", "Clothes_Tee" }, { "Hair", "Hair_Yellow" } };
            var made = new Dictionary<string, Material>();
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                if (r is SkinnedMeshRenderer s) { smrs.Add(s); s.updateWhenOffscreen = true; }
                if (r.name == "StarPin") starPin = r.gameObject;
                var src = r.sharedMaterials; var dst = new Material[src.Length];
                for (int i = 0; i < src.Length; i++)
                {
                    var m = src[i]; string n = m != null ? m.name : "";
                    if (!made.TryGetValue(n, out var nm))
                    {
                        if (texOf.TryGetValue(n, out var tn) && toonSh != null)
                        {
                            nm = new Material(toonSh) { name = n }; nm.SetTexture("_MainTex", Resources.Load<Texture2D>("Girl/" + tn));
                            if (n == "Clothes_Tee") teeMat = nm;
                        }
                        else if (n == "Emission") nm = Flat(new Color(0.09f, 0.045f, 0.05f), 1f);         // the ink outline shell
                        else
                        {
                            Color c = Color.white; if (m != null) { if (m.HasProperty("_BaseColor")) c = m.GetColor("_BaseColor"); else if (m.HasProperty("_Color")) c = m.GetColor("_Color"); }
                            if (n.StartsWith("Star")) c = new Color(1f, 0.96f, 0.84f);
                            nm = Flat(c, 0.9f);
                        }
                        made[n] = nm;
                    }
                    dst[i] = nm;
                }
                r.sharedMaterials = dst;
            }
        }

        void BuildBubble()
        {
            bubble = UIKit.WorldCanvas("VeeBubble", 760, 230, null, 0.0007f); var rt = (RectTransform)bubble.transform;
            var back = UIKit.Box(rt, "Back", new Color(1f, 0.98f, 0.93f, 0.96f), 34, false); UIKit.Fill(back.rectTransform);
            UIKit.Outline(back.transform, C("#ff9fc0"), 34, 5f);
            UIKit.TextAt(rt, "VEE", 28, 30, 12, 200, 40, C("#e0457b"), TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            bubbleText = UIKit.TextAt(rt, "", 31, 30, 54, 700, 165, C("#2a2438"), TextAlignmentOptions.TopLeft);
            bubbleText.textWrappingMode = TextWrappingModes.Normal; bubbleText.overflowMode = TextOverflowModes.Truncate;
            bubble.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- outfits
        void SetOutfit(string cat)
        {
            if (outfitRoot != null) Destroy(outfitRoot); outfitRoot = null;
            foreach (var pc in outfitPieces) if (pc != null) Destroy(pc); outfitPieces.Clear();
            foreach (var kv in rest) kv.Key.localRotation = kv.Value;            // rest pose, so the pieces (authored in rest pose) land exactly on their bones
            bool covered = false;
            if (!string.IsNullOrEmpty(cat) && Looks.TryGetValue(cat, out var look))
            {
                if (teeMat != null) { teeMat.SetFloat("_Recolor", 1f); teeMat.SetColor("_TeeColor", C(look.tee)); teeMat.SetColor("_SleeveColor", C(look.sleeve)); }
                var prefab = Resources.Load<GameObject>("Girl/Outfits/Outfit_" + cat);
                if (prefab != null)
                {
                    outfitRoot = new GameObject("Outfit_" + cat); outfitRoot.transform.SetParent(model, false);
                    var inst = Instantiate(prefab, model); inst.transform.localPosition = Vector3.zero; inst.transform.localRotation = Quaternion.identity; inst.transform.localScale = Vector3.one;
                    foreach (var mf in inst.GetComponentsInChildren<MeshFilter>(true).ToList())
                    {
                        var t = mf.transform; int ix = t.name.IndexOf("__"); string bn = ix >= 0 ? t.name.Substring(ix + 2) : "Head";
                        foreach (var k in HeadCovers) if (t.name.Contains(k)) covered = true;
                        var r = t.GetComponent<Renderer>(); var ms = r.sharedMaterials;
                        for (int i = 0; i < ms.Length; i++) ms[i] = Flat(ColorOf(ms[i] != null ? ms[i].name : ""), 0.8f);
                        r.sharedMaterials = ms; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        t.SetParent(bones.TryGetValue(bn, out var bone) ? bone : outfitRoot.transform, true); outfitPieces.Add(t.gameObject);
                    }
                    Destroy(inst);
                }
            }
            else if (teeMat != null) teeMat.SetFloat("_Recolor", 0f);
            if (starPin != null) starPin.SetActive(!covered);
        }

        // ---------------------------------------------------------------- speech + lip sync
        public void Speak(string text, float hold = 0f)
        {
            speech = text ?? ""; speechStart = Time.time; speechEnd = Time.time + Mathf.Clamp(speech.Length * 0.07f, 2f, 10f) + hold;
            if (bubbleText != null) bubbleText.text = speech;
            VoiceOut.Say(speech, gameObject);
        }
        public bool Speaking { get { return Time.time < speechEnd; } }

        static string Viseme(char ch)
        {
            switch (char.ToLowerInvariant(ch))
            {
                case 'a': case 'e': case 'i': return "A,E,I";
                case 'o': case 'u': return "O,U";
                case 'b': case 'm': case 'p': return "B,M,P";
                case 'f': case 'v': return "F,V";
                case 'l': return "L";
                case 'j': case 'h': return "CH,J,SH,TH";
                case 'q': case 'w': return "Q,W";
                case 'g': case 'k': return "G,K";
                case 'c': case 'd': case 'n': case 's': case 't': case 'x': case 'y': case 'z': case 'r': return "C,D,N,S,T,X,Y,Z";
                default: return "";
            }
        }
        static readonly string[] AllVis = { "A,E,I", "C,D,N,S,T,X,Y,Z", "G,K", "O,U", "Q,W", "CH,J,SH,TH", "F,V", "B,M,P", "L" };
        readonly Dictionary<string, float> vw = new Dictionary<string, float>();
        void SetShape(string key, float w)
        {
            foreach (var s in smrs)
            {
                var m = s.sharedMesh; if (m == null) continue; int i = m.GetBlendShapeIndex(key); if (i < 0) i = m.GetBlendShapeIndex(key.Replace(',', '_')); if (i >= 0) s.SetBlendShapeWeight(i, w);
            }
        }
        void TickFace()
        {
            string want = "";
            if (Speaking && speech.Length > 0)
            {
                int ci = Mathf.Clamp(Mathf.FloorToInt((Time.time - speechStart) * 14f), 0, speech.Length - 1); want = Viseme(speech[ci]);
            }
            foreach (var k in AllVis)
            {
                vw.TryGetValue(k, out var cur); float tgt = k == want ? 70f : 0f; cur = Mathf.Lerp(cur, tgt, 1f - Mathf.Exp(-22f * Time.deltaTime)); vw[k] = cur; SetShape(k, cur);
            }
            if (Time.time > blinkAt && blinkT < 0f) { blinkT = 0f; blinkAt = Time.time + Random.Range(2.5f, 5f); }
            float bw = 0f; if (blinkT >= 0f) { blinkT += Time.deltaTime; bw = Mathf.Sin(Mathf.Clamp01(blinkT / 0.18f) * Mathf.PI) * 100f; if (blinkT > 0.18f) blinkT = -1f; }
            SetShape("Eye L", bw); SetShape("Eye R", bw);
        }


        // ---------------------------------------------------------------- props for the task she re-enacts (same 3D set Stara uses: pan, knife, bicycle, surfboard ...)
        Transform propRoot; readonly Dictionary<string, GameObject> props = new Dictionary<string, GameObject>(); GameObject curProp;
        void BuildProps()
        {
            if (propRoot != null) return;
            var prefab = Resources.Load<GameObject>("Mascot/Activities"); propRoot = new GameObject("TaskProps").transform; propRoot.SetParent(transform, false); if (prefab == null) return;
            var inst = Instantiate(prefab, propRoot); inst.transform.localPosition = Vector3.zero; inst.transform.localRotation = Quaternion.identity; inst.transform.localScale = Vector3.one;
            var cache = new Dictionary<Material, Material>();
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                var src = r.sharedMaterials; var dst = new Material[src.Length];
                for (int k = 0; k < src.Length; k++)
                {
                    var m = src[k]; if (m == null) continue;
                    if (!cache.TryGetValue(m, out var nm)) { Color c = Color.white; if (m.HasProperty("_BaseColor")) c = m.GetColor("_BaseColor"); else if (m.HasProperty("_Color")) c = m.GetColor("_Color"); nm = Flat(c, 1f); cache[m] = nm; }
                    dst[k] = nm;
                }
                r.sharedMaterials = dst;
            }
            foreach (Transform t in inst.transform) { if (t.name.StartsWith("A_")) props[t.name] = t.gameObject; t.gameObject.SetActive(false); }
        }
        void ShowProp(string name)
        {
            BuildProps(); if (curProp != null) curProp.SetActive(false); curProp = null;
            if (name != null && props.TryGetValue(name, out var go)) { curProp = go; go.SetActive(true); }
            Debug.Log("[Vee] prop=" + name + " found=" + (curProp != null) + " total=" + props.Count + " names=" + string.Join(",", props.Keys));
        }
        void TickProp()
        {
            if (curProp == null || propRoot == null) return;
            propRoot.localScale = Vector3.one * 0.9f; propRoot.localPosition = new Vector3(0.75f, 0f, 0.3f);          // beside her, on the floor, where her hands can reach it
            curProp.transform.localRotation = Quaternion.Euler(0, -25f + Mathf.Sin(Time.time * 2f) * 3f, 0);
        }

        // ---------------------------------------------------------------- helpers for the re-enactment stage
        public Material Mat(Color c) { return Flat(c, 0.92f); }
        public Transform HandBone(bool right) { return right ? handR : handL; }
        public Vector3 RepickSpot(bool close)
        {
            closeUp = close; var cam = Camera.main; if (cam == null) return transform.position;
            bool hudOn = journey != null && journey.Hud != null && journey.Hud.Visible && journey.Hud.Root != null;
            Vector3 fpos = hudOn ? journey.Hud.Root.position : cam.transform.position + cam.transform.forward * 1.5f;
            wantPos = PickSpot(cam, fpos, !hudOn); wantPos.y = floorY; return wantPos;
        }
        bool forceStage;
        public void TestAct(string verb, string noun) { forceStage = true; ActAs(verb, noun); }          // editor preview helper
        public void TestStop() { forceStage = false; ActAs(null, null); if (reen != null) reen.End(); }
        public void ReleasePlacement() { closeUp = false; lastMode = -1; }

        // ---------------------------------------------------------------- re-enactment hooks
        public void ActAs(string verb, string noun)
        {
            string cat = AppState.Category;
            if (verb == null) { gesture = "Idle"; gestureLabel = null; ShowProp(null); if (reen != null) reen.Idle(); return; }
            if (reen != null && !string.IsNullOrEmpty(cat)) { reen.Begin(verb, noun); gesture = "Idle"; gestureLabel = verb + " " + noun; ShowProp(null); return; }
            string g = VerbGesture(verb);
            if (cat == "Boxing") g = "Punch"; else if (cat == "Plane") g = "Fly"; else if (cat == "RockClimbing" || cat == "Parkour") g = "Climb";
            else if (cat != null && (cat == "Bike" || cat == "BikePolo" || cat == "Motor" || cat == "Skate" || cat == "Longboarding" || cat == "Ski" || cat == "Surfing" || cat == "Horse")) g = "Ride";
            else if (cat == "Scuba") g = "Swim";
            gesture = g; gestureT = 0f; gestureLabel = verb + " " + noun; ShowProp(MascotAssistant.PropFor(cat, noun));
        }
        static string VerbGesture(string v)
        {
            v = (v ?? "").ToLowerInvariant();
            if (v.StartsWith("cut") || v.StartsWith("slice") || v.StartsWith("peel") || v.StartsWith("chop")) return "Chop";
            if (v.StartsWith("mix") || v.StartsWith("stir") || v.StartsWith("wash") || v.StartsWith("wipe") || v.StartsWith("clean") || v.StartsWith("spread")) return "Stir";
            if (v.StartsWith("pour") || v.StartsWith("drink") || v.StartsWith("fill") || v.StartsWith("shake")) return "Pour";
            return "Reach";
        }

        // ---------------------------------------------------------------- update: placement + state
        void Update()
        {
            if (model == null) return;
            var cam = Camera.main; if (cam == null) return;
            if (journey == null) journey = FindFirstObjectByType<JourneyController>();
            string cat = AppState.Category;
            if (cat != shownCat) { bool first = shownCat == null; shownCat = cat ?? ""; rescanAt = Time.time + (first ? 0.6f : 1.5f); if (!string.IsNullOrEmpty(cat)) { SetOutfit(cat); OnCategory(cat); } else SetOutfit(null); }
            if (AppState.HasResult != hadResult) { hadResult = AppState.HasResult; if (hadResult) { StaraChat("Come closer and watch what Vee does. She acts out exactly what happened in your video!"); gesture = "Wave"; gestureT = 0f; } else if (reen != null) reen.End(); }
            if (cat == null || cat == "") { if (Time.time > nextIdleChat && !Speaking) { nextIdleChat = Time.time + 14f; Speak(MenuLines[Random.Range(0, MenuLines.Length)]); gesture = "Wave"; gestureT = 0f; } }

            { bool hudNow = journey != null && journey.Hud != null && journey.Hud.Visible; if (!hudNow && !forceStage && reen != null && reen.Active) reen.End(); }
            Place(cam); TickProp();
            if (Time.time > dbgAt) { dbgAt = Time.time + 6f; var rb = model.GetComponentsInChildren<Renderer>(); Debug.Log("[Vee] pos=" + transform.position + " cam=" + cam.transform.position + " fwd=" + cam.transform.forward + " rend=" + rb.Length + " vis=" + rb.Count(r => r.isVisible) + " scale=" + model.lossyScale + " bones=" + bones.Count + " cat=" + shownCat); }
            if (bubble != null)
            {
                bool on = Speaking; if (bubble.gameObject.activeSelf != on) bubble.gameObject.SetActive(on);
                if (on)
                {
                    float dist = Vector3.Distance(cam.transform.position, transform.position); float sc = Mathf.Clamp(dist * 0.00042f, 0.0007f, 0.0016f);
                    var bp = transform.position + Vector3.up * (1.78f * modelScale + 0.12f + 230f * sc * 0.5f);          // floats just above her head
                    bubble.transform.position = bp; bubble.transform.rotation = Quaternion.LookRotation(bp - cam.transform.position, Vector3.up); bubble.transform.localScale = Vector3.one * sc;
                    bubbleText.maxVisibleCharacters = Mathf.FloorToInt((Time.time - speechStart) * 40f);
                }
            }
            if (gesture != "Idle" && gesture != "Wave" && Speaking == false && gestureLabel == null) gesture = "Idle";
            gestureT += Time.deltaTime; if (gesture == "Wave" && gestureT > 3.2f) gesture = "Idle";
        }

        static readonly string[] MenuLines = {
            "Hi! I'm Vee. Pick a category and I'll change into the right outfit!",
            "Tap a category card, then choose a video. I'll act out what happens in it.",
            "Stara and I will show you the key moments once the analysis is done." };

        void OnCategory(string cat)
        {
            string line = Looks.TryGetValue(cat, out var l) ? l.line : "New look!";
            Speak(line); gesture = "Wave"; gestureT = 0f;

        }
        void StaraChat(string s) { var m = MascotAssistant.Instance; if (m != null) m.Chat(s); }

        Vector3 lastFramePos; int lastMode = -1; Vector3 wantPos; float rescanAt = -1f; readonly List<Bounds> obstacles = new List<Bounds>();
        float Clear { get { return closeUp ? 1.5f : 0.5f; } }            // keep this far from props and walls (the re-enactment stage needs more room)

        void ScanObstacles()
        {
            obstacles.Clear(); var env = CategoryEnvironment.Instance; if (env == null) return;
            foreach (var r in env.GetComponentsInChildren<Renderer>())
            {
                if (r.name == "Sky" || r.GetComponentInParent<GuideGirl>() != null) continue; var b = r.bounds;
                if (b.size.y < 0.12f || b.max.y < 0.12f || b.min.y > 1.7f) continue;               // floor decals, things overhead
                if (b.size.x > 40f || b.size.z > 40f) continue;                                      // the ground itself
                obstacles.Add(b);
            }
        }
        bool Free(Vector3 p)
        {
            foreach (var b in obstacles) if (p.x > b.min.x - Clear && p.x < b.max.x + Clear && p.z > b.min.z - Clear && p.z < b.max.z + Clear) return false;
            return true;
        }
        float Margin(Vector3 p) { float m = 5f; foreach (var b in obstacles) { float dx = Mathf.Max(b.min.x - p.x, 0f, p.x - b.max.x), dz = Mathf.Max(b.min.z - p.z, 0f, p.z - b.max.z); m = Mathf.Min(m, Mathf.Sqrt(dx * dx + dz * dz)); } return m; }

        Vector3 PickSpot(Camera cam, Vector3 fpos, bool preferRight)
        {
            Vector3 camF = cam.transform.position; Vector3 dir = fpos - camF; dir.y = 0; if (dir.sqrMagnitude < 0.01f) { dir = cam.transform.forward; dir.y = 0; } dir.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, dir); Vector3 baseP = new Vector3(camF.x, floorY, camF.z);
            float[] dists = closeUp ? new[] { 1.7f, 1.5f, 1.9f, 2.2f, 2.6f } : new[] { 2.3f, 2.0f, 2.7f, 1.8f, 3.1f, 3.6f }; float[] lat = closeUp ? new[] { 0.9f, 0.6f, 1.2f, 1.6f, 2.0f } : new[] { 1.35f, 1.0f, 1.7f, 2.1f, 2.6f };
            Vector3 best = baseP + dir * 2.3f + right * (preferRight ? 1.35f : -1.35f); float bestM = -1f;
            foreach (float sgn in new[] { preferRight ? 1f : -1f, preferRight ? -1f : 1f })
                foreach (float d in dists) foreach (float l in lat)
                    {
                        Vector3 c = baseP + dir * d + right * (sgn * l);
                        Vector3 toC = c - baseP; toC.y = 0; Vector3 cf = cam.transform.forward; cf.y = 0;
                        if (Vector3.Angle(cf, toC) > 50f) continue;                                    // keep her where the user is looking
                        if (Free(c)) return c;
                        float m = Margin(c); if (m > bestM) { bestM = m; best = c; }
                    }
            return best;
        }

        void Place(Camera cam)
        {
            bool hudOn = journey != null && journey.Hud != null && journey.Hud.Visible && journey.Hud.Root != null;
            bool stage = !hudOn && journey != null && journey.Stage != null;
            int mode = hudOn ? 1 : stage ? 0 : 2;
            Vector3 fpos = hudOn ? journey.Hud.Root.position : stage ? journey.transform.position : cam.transform.position + cam.transform.forward * 1.2f;
            bool snap = false;
            if (rescanAt > 0f && Time.time >= rescanAt) { rescanAt = -1f; ScanObstacles(); lastMode = -1; snap = true; }          // the world was swapped under the shift: re-pick her spot and put her there
            if (mode != lastMode || (fpos - lastFramePos).sqrMagnitude > 0.04f || !placed)
            {
                lastMode = mode; lastFramePos = fpos;
                wantPos = PickSpot(cam, fpos, mode != 1); wantPos.y = floorY;           // menu: right of the panel, results: left (Stara is on the right)
            }
            Vector3 want = wantPos; bool stagedNow = reen != null && reen.Active; if (stagedNow) want = reen.stand;
            if (!placed || snap) { transform.position = want; placed = true; }
            Vector3 d = want - transform.position; d.y = 0; float dist = d.magnitude; bool walking = dist > 0.03f;
            if (stagedNow && reen.direct) { var q = transform.position; transform.position = new Vector3(want.x, q.y, want.z); walking = false; }
            else if (walking) { transform.position += d.normalized * Mathf.Min(dist, (stagedNow ? 1.4f : 0.9f) * Time.deltaTime); walkPh += Time.deltaTime * 7f; }
            { var q2 = transform.position; float ty = stagedNow ? want.y : floorY; q2.y = reen != null && stagedNow && reen.direct ? Mathf.Lerp(q2.y, ty, 1f - Mathf.Exp(-14f * Time.deltaTime)) : Mathf.MoveTowards(q2.y, ty, 1.2f * Time.deltaTime); transform.position = q2; }
            Vector3 face = walking && dist > 0.25f ? d.normalized : (cam.transform.position - transform.position); face.y = 0;
            if (stagedNow) { Vector3 toCam = cam.transform.position - transform.position; toCam.y = 0; face = Vector3.Slerp(reen.faceDir, toCam.normalized, reen.faceCam); face.y = 0; if (face.sqrMagnitude < 1e-4f) face = reen.faceDir; }
            if (face.sqrMagnitude > 0.001f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face.normalized, Vector3.up), 1f - Exp6());
            walkingNow = walking;
        }
        static float Exp6() { return Mathf.Exp(-6f * Time.deltaTime); }
        bool walkingNow; float dbgAt;

        // ---------------------------------------------------------------- procedural pose (after the animator would run)
        void PointLimb(Transform bone, Transform child, Vector3 dirWorld)
        {
            if (bone == null || child == null) return;
            Vector3 cur = child.position - bone.position; if (cur.sqrMagnitude < 1e-8f || dirWorld.sqrMagnitude < 1e-8f) return;
            bone.rotation = Quaternion.FromToRotation(cur, dirWorld.normalized) * bone.rotation;
        }
        void LegPose(int mode, float ph)
        {
            Vector3 fw = transform.forward, up = Vector3.up;
            for (int side = 0; side < 2; side++)
            {
                Transform th = side == 0 ? thighL : thighR, sh = side == 0 ? shinL : shinR, ft = side == 0 ? footL : footR; if (th == null || sh == null || ft == null) continue;
                Vector3 o = Out(th); float a = ph + (side == 0 ? 0f : Mathf.PI); Vector3 td, sd;
                switch (mode)
                {
                    case 1: td = fw * 0.55f - up * 0.65f + o * 0.55f; sd = -up * 0.95f - fw * 0.1f + o * 0.2f; break;                                    // astride (horse)
                    case 2: td = fw * 0.9f + up * (0.1f + 0.5f * Mathf.Sin(a)) + o * 0.12f; sd = -up * 0.9f - fw * (0.2f - 0.3f * Mathf.Cos(a)) + o * 0.05f; break;     // pedalling
                    case 3: { float k = Mathf.Max(0f, Mathf.Sin(a)); td = fw * (0.15f + 0.6f * k) - up * (1f - 0.7f * k) + o * 0.15f; sd = -up * (0.9f - 0.5f * k) - fw * 0.1f + o * 0.05f; break; }          // climbing
                    case 4: td = fw * 0.8f - up * 0.45f + o * 0.1f; sd = -fw * 0.35f - up * 0.65f; break;                                              // jumping, tucked
                    default: td = -up * 0.95f + fw * 0.15f * Mathf.Sin(a) + o * 0.05f; sd = -up * 0.95f - fw * 0.3f * Mathf.Max(0f, Mathf.Sin(a + 1f)); break;      // swimming kick
                }
                PointLimb(th, sh, td); PointLimb(sh, ft, sd);
            }
        }
        void TwoBone(Transform u, Transform f, Transform h, Vector3 target, Vector3 pole)
        {
            if (u == null || f == null || h == null) return;
            float L1 = (f.position - u.position).magnitude, L2 = (h.position - f.position).magnitude; Vector3 S = u.position; Vector3 dv = target - S; float dist = Mathf.Clamp(dv.magnitude, 0.05f, L1 + L2 - 0.004f); Vector3 dir = dv.normalized;
            Vector3 T = S + dir * dist; float a = (L1 * L1 - L2 * L2 + dist * dist) / (2f * dist); float hh = Mathf.Sqrt(Mathf.Max(0f, L1 * L1 - a * a));
            Vector3 perp = pole - Vector3.Project(pole, dir); if (perp.sqrMagnitude < 1e-6f) perp = Vector3.down; perp.Normalize(); Vector3 E = S + dir * a + perp * hh;
            PointLimb(u, f, E - S); PointLimb(f, h, T - E);
        }
        Vector3 Out(Transform t) { float sgn = Mathf.Sign(Vector3.Dot(t.position - transform.position, transform.right)); if (Mathf.Abs(sgn) < 0.5f) sgn = 1f; return transform.right * sgn; }

        void LateUpdate()
        {
            if (model == null) return;
            foreach (var kv in rest) kv.Key.localRotation = kv.Value;
            TickFace();
            float t = Time.time; bool talk = Speaking; Vector3 fw = transform.forward, up = Vector3.up;
            // body: breathing + talking bob
            if (spine != null) spine.rotation = Quaternion.AngleAxis(Mathf.Sin(t * 1.7f) * 1.2f + (talk ? Mathf.Sin(t * 5f) * 1.2f : 0f), transform.right) * spine.rotation;
            // head: look at the camera a little
            var cam = Camera.main;
            if (head != null && cam != null)
            {
                Vector3 to = cam.transform.position - head.position; Vector3 lf = Vector3.ProjectOnPlane(to, up); float yawA = Mathf.Clamp(Vector3.SignedAngle(fw, lf, up), -40f, 40f) * 0.6f;
                head.rotation = Quaternion.AngleAxis(yawA, up) * Quaternion.AngleAxis(Mathf.Sin(t * 1.3f) * 2f + (talk ? Mathf.Sin(t * 6f) * 2.5f : 0f), transform.right) * head.rotation;
            }
            // hair tails sway
            foreach (var n in bones.Keys) if (n.StartsWith("Tail.") || n.StartsWith("Hair ")) { var b = bones[n]; if (rest.ContainsKey(b)) b.localRotation = rest[b] * Quaternion.Euler(Mathf.Sin(t * 2f + b.position.y * 6f) * 2.2f, 0, Mathf.Cos(t * 1.6f + b.position.y * 5f) * 2.2f + (walkingNow ? Mathf.Sin(walkPh * 2f) * 3f : 0f)); }
            // arms
            float gt = gestureT; string g = gesture;
            foreach (var side in new[] { 0, 1 })
            {
                Transform u = side == 0 ? armUL : armUR, f = side == 0 ? armFL : armFR, h = side == 0 ? handL : handR; if (u == null) continue;
                Vector3 o = Out(u); Vector3 ud = (o * 0.28f - up).normalized, fd = (o * 0.12f - up + fw * 0.18f).normalized;      // relaxed, hanging
                bool right = (side == 1); float ph = right ? 0f : Mathf.PI;
                if (reen != null && reen.Active && (right ? reen.hasR : reen.hasL)) { TwoBone(u, f, h, right ? reen.tR : reen.tL, (o * 0.6f - up - fw * 0.3f).normalized); continue; }
                if (walkingNow) { ud = (o * 0.2f - up + fw * Mathf.Sin(walkPh + ph) * 0.5f).normalized; fd = (o * 0.1f - up + fw * (0.2f + Mathf.Sin(walkPh + ph) * 0.35f)).normalized; }
                else if (g == "Wave" && right) { ud = (o * 0.8f + up * 0.6f).normalized; fd = (o * 0.2f + up + fw * 0.1f + o * Mathf.Sin(t * 9f) * 0.7f).normalized; }
                else if (g == "Reach") { float k = 0.5f + 0.5f * Mathf.Sin(t * 2.2f + ph * 0.3f); ud = (o * 0.2f + fw * 0.8f - up * 0.3f).normalized; fd = (fw + up * 0.15f * k - o * 0.1f).normalized; }
                else if (g == "Chop") { float k = Mathf.Sin(t * 8f + (right ? 0f : 1f)); ud = (o * 0.15f + fw * 0.7f - up * 0.3f).normalized; fd = (fw * 0.6f + up * (right ? k : -0.1f) * 0.9f).normalized; }
                else if (g == "Stir") { float a = t * 6f; ud = (o * 0.15f + fw * 0.7f - up * 0.35f).normalized; fd = (fw * 0.8f + o * Mathf.Cos(a) * 0.35f * (right ? 1f : 0.3f) + up * Mathf.Sin(a) * 0.3f * (right ? 1f : 0.3f) - up * 0.1f).normalized; }
                else if (g == "Pour") { ud = (o * 0.2f + fw * 0.6f - up * 0.4f).normalized; fd = (fw * 0.5f + up * (right ? 0.7f : 0.1f) + o * 0.2f).normalized; }
                else if (g == "Punch") { float k = Mathf.Sin(t * 7f + (right ? 0f : Mathf.PI)); float ext = Mathf.Clamp01(k * 0.5f + 0.5f); ud = (o * 0.15f + fw * (0.4f + 0.5f * ext) - up * 0.15f).normalized; fd = (fw * (0.6f + 0.6f * ext) + up * 0.1f).normalized; }
                else if (g == "Ride") { ud = (o * 0.25f + fw * 0.8f - up * 0.4f).normalized; fd = (fw * 0.9f - o * 0.05f - up * 0.2f).normalized; }
                else if (g == "Climb") { float k = Mathf.Sin(t * 3f + ph); ud = (o * 0.25f + up * (0.6f + 0.4f * k)).normalized; fd = (up * (0.8f + 0.2f * k) + fw * 0.2f).normalized; }
                else if (g == "Fly") { ud = (o * 1f + up * 0.1f * Mathf.Sin(t * 3f)).normalized; fd = (o * 1f + up * 0.15f * Mathf.Sin(t * 3f + 0.6f)).normalized; }
                else if (g == "Swim") { float k = Mathf.Sin(t * 3f + ph); ud = (o * 0.7f + fw * 0.6f).normalized; fd = (fw * 0.8f + o * 0.5f * k).normalized; }
                else if (talk) { float k = Mathf.Sin(t * 3.2f + ph * 0.6f); ud = (o * 0.3f + fw * 0.4f - up * 0.7f).normalized; fd = (fw * 0.55f + up * (0.3f + 0.35f * k) - o * 0.1f).normalized; }
                PointLimb(u, f, ud); PointLimb(f, h, fd);
            }
            // legs: walk swing
            int lm = reen != null && reen.Active ? reen.legMode : 0;
            if (lm != 0) LegPose(lm, reen.legPhase);
            else if (thighL != null && thighR != null)
            {
                float sw = walkingNow ? Mathf.Sin(walkPh) * 28f : (g == "Ride" || g == "Swim" ? Mathf.Sin(t * 5f) * 18f : 0f);
                thighL.rotation = Quaternion.AngleAxis(sw, transform.right) * thighL.rotation; thighR.rotation = Quaternion.AngleAxis(-sw, transform.right) * thighR.rotation;
            }
            if (reen != null) reen.AfterPose();
            if (model != null) model.localPosition = new Vector3(0, walkingNow ? Mathf.Abs(Mathf.Sin(walkPh)) * 0.02f : (g == "Climb" || g == "Fly" ? 0.02f + Mathf.Sin(t * 3f) * 0.015f : 0f), 0);
        }
    }
}
