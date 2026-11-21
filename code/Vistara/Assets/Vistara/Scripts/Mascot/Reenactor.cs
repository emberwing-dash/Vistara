using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vistara
{
    /// <summary>
    /// Vee's kitchen: a small stylised kitchen run (fridge | cupboard | stove | sink) she uses to re-enact, step by step, the exact action found in the video:
    /// she opens the cupboard, takes things out of the fridge and shows them, chops, stirs, washes, turns the stove on, pours ... with cooking sounds (fire, sizzle, chop, water, doors).
    /// The stage only outputs hand targets / where she stands; GuideGirl turns that into arm IK.
    /// </summary>
    public class Reenactor : MonoBehaviour
    {
        enum Kind { None, OpenClose, Take, Put, Cut, Stir, Wash, Knob, Pour, Drink, Show }
        class Door { public Transform pivot, handle; public float angle; public float x; }

        GuideGirl girl; Transform stage; bool built;
        public string Mode = "move";          // what the video shows right now (from the analysis): move, fast, turn, stop, jump, hit, climb, swim, idle
        public float Tempo = 1f;          // how fast the movement is performed: follows how much is going on in the video at that moment
        public bool Active { get; private set; }
        public string CurVerb { get { return verb; } }
        public string CurNoun { get { return noun; } }
        public Vector3 stand, faceDir; public float faceCam; public bool hasR, hasL; public Vector3 tR, tL;
        Door fridge, cabinet; Transform flames, water, knob, board, slices; GameObject held, boardVeg, potProp, cupProp; readonly List<Transform> flameCones = new List<Transform>();
        public int legMode; public float legPhase; public bool direct; SportStage sport; bool sportMode; string sportCat; Transform frameT;
        Kind kind; float t; string verb = "", noun = ""; bool closeFirst; float itemTilt; Vector3 itemOff; bool itemOn;
        Vector3 origin; Quaternion frameRot; Door curDoor; float standX; bool flameOn, waterOn, pourOn; float prevChop; int lastSfxSeg = -1;

        const float ZF = 0.26f;                       // front plane of the kitchen units (distance from where she stands)
        const float FX = -0.70f, CX = -0.20f, SX = 0.30f, KX = 0.80f;        // fridge, cupboard, stove, sink (centre x along the row)

        void Awake() { girl = GetComponent<GuideGirl>(); sport = new SportStage(this, girl); }
        public bool CarryingItem { get { return itemOn && held != null; } }
        public void Carry(GameObject go, bool on, float tilt) { held = go; itemOn = on; itemTilt = tilt; }

        // ---------------------------------------------------------------- stage geometry
        GameObject Prim(Transform parent, PrimitiveType type, Vector3 lp, Vector3 ls, string hex, Vector3? euler = null)
        {
            var g = GameObject.CreatePrimitive(type); var c = g.GetComponent<Collider>(); if (c != null) Destroy(c);
            g.transform.SetParent(parent, false); g.transform.localPosition = lp; g.transform.localScale = ls; if (euler.HasValue) g.transform.localRotation = Quaternion.Euler(euler.Value);
            g.GetComponent<Renderer>().sharedMaterial = girl.Mat(Col(hex)); g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; return g;
        }
        static Color Col(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
        GameObject Box(Transform p, float x, float y, float z, float w, float h, float d, string hex) { return Prim(p, PrimitiveType.Cube, new Vector3(x, y, z), new Vector3(w, h, d), hex); }
        GameObject Cyl(Transform p, float x, float y, float z, float r, float h, string hex, Vector3? e = null) { return Prim(p, PrimitiveType.Cylinder, new Vector3(x, y, z), new Vector3(r * 2, h / 2, r * 2), hex, e); }
        GameObject Ball(Transform p, float x, float y, float z, float sx, float sy, float sz, string hex) { return Prim(p, PrimitiveType.Sphere, new Vector3(x, y, z), new Vector3(sx, sy, sz), hex); }

        Door Unit(string name, float cx, float w, float h, float d, string body, string inner, float[] shelves)
        {
            var root = new GameObject(name).transform; root.SetParent(stage, false); float zc = ZF + d / 2f; const float th = 0.03f;
            Box(root, cx, h / 2, ZF + d - th / 2, w, h, th, inner); Box(root, cx - w / 2 + th / 2, h / 2, zc, th, h, d, body); Box(root, cx + w / 2 - th / 2, h / 2, zc, th, h, d, body);
            Box(root, cx, th / 2, zc, w, th, d, inner); Box(root, cx, h - th / 2, zc, w, th, d, body);
            foreach (float y in shelves) Box(root, cx, y, zc, w - 2 * th, 0.02f, d - 0.04f, inner);
            var door = new Door { x = cx }; var pv = new GameObject(name + "Door").transform; pv.SetParent(root, false); pv.localPosition = new Vector3(cx - w / 2, 0, ZF); door.pivot = pv;
            Box(pv, w / 2, h / 2, -0.015f, w - 0.01f, h - 0.01f, 0.03f, body); var hd = Box(pv, w - 0.07f, h * 0.6f, -0.045f, 0.025f, Mathf.Min(0.2f, h * 0.3f), 0.03f, "#3a3f4a"); door.handle = hd.transform; return door;
        }

        void Build()
        {
            built = true; stage = new GameObject("VeeKitchen").transform;
            Box(stage, 0.05f, 0.8f, ZF + 0.5f, 2.3f, 1.6f, 0.04f, "#f3ece0");                                     // back wall
            Box(stage, 0.05f, 0.005f, ZF + 0.22f, 2.3f, 0.01f, 0.9f, "#d8c9b0");                                   // floor mat
            fridge = Unit("Fridge", FX, 0.5f, 1.0f, 0.45f, "#e8eef2", "#f9fcff", new[] { 0.34f, 0.66f });
            cabinet = Unit("Cupboard", CX, 0.5f, 0.5f, 0.42f, "#c58f5c", "#e8d3b4", new[] { 0.27f });
            Box(stage, SX, 0.25f, ZF + 0.21f, 0.5f, 0.5f, 0.42f, "#d9dde3"); Box(stage, KX, 0.25f, ZF + 0.21f, 0.5f, 0.5f, 0.42f, "#cfd6dc");   // stove + sink units
            Box(stage, (SX + KX) / 2f, 0.515f, ZF + 0.215f, 1.0f, 0.03f, 0.44f, "#b98a5b");                          // counter top over stove/sink
            Box(stage, CX, 0.515f, ZF + 0.215f, 0.5f, 0.03f, 0.44f, "#b98a5b");
            // stove: burner, knob, flames
            Cyl(stage, SX, 0.535f, ZF + 0.21f, 0.10f, 0.014f, "#23252d"); Cyl(stage, SX, 0.545f, ZF + 0.21f, 0.05f, 0.014f, "#3a3d48");
            knob = Cyl(stage, SX + 0.12f, 0.40f, ZF - 0.01f, 0.026f, 0.03f, "#2b2d36", new Vector3(90, 0, 0)).transform; Box(knob, 0, 0, 0, 0.3f, 0.3f, 1.6f, "#ff7a1a");
            flames = new GameObject("Flames").transform; flames.SetParent(stage, false); flames.localPosition = new Vector3(SX, 0.545f, ZF + 0.21f);
            for (int i = 0; i < 8; i++) { float a = i * Mathf.PI * 2 / 8; var c = Prim(flames, PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * 0.07f, 0.02f, Mathf.Sin(a) * 0.07f), new Vector3(0.025f, 0.06f, 0.025f), i % 2 == 0 ? "#ff8a1f" : "#ffd84a"); flameCones.Add(c.transform); }
            flames.gameObject.SetActive(false);
            // sink: basin, tap, water
            Box(stage, KX, 0.535f, ZF + 0.21f, 0.36f, 0.02f, 0.28f, "#5d6b78"); Cyl(stage, KX, 0.64f, ZF + 0.36f, 0.014f, 0.22f, "#c9d1d8"); Box(stage, KX, 0.74f, ZF + 0.29f, 0.022f, 0.022f, 0.16f, "#c9d1d8");
            water = Cyl(stage, KX, 0.64f, ZF + 0.215f, 0.009f, 0.2f, "#8fd8ff").transform; water.gameObject.SetActive(false);
            // props that stay on the counter
            potProp = new GameObject("Pot").transform.gameObject; potProp.transform.SetParent(stage, false); potProp.transform.localPosition = new Vector3(SX, 0.56f, ZF + 0.21f);
            Cyl(potProp.transform, 0, 0.045f, 0, 0.085f, 0.09f, "#aab3bd"); Cyl(potProp.transform, 0, 0.088f, 0, 0.07f, 0.004f, "#e8a45a"); Box(potProp.transform, -0.12f, 0.06f, 0, 0.07f, 0.014f, 0.018f, "#23252d"); Box(potProp.transform, 0.12f, 0.06f, 0, 0.07f, 0.014f, 0.018f, "#23252d"); potProp.SetActive(false);
            board = new GameObject("Board").transform; board.SetParent(stage, false); board.localPosition = new Vector3(CX, 0.535f, ZF + 0.12f);
            Box(board, 0, 0.008f, 0, 0.28f, 0.016f, 0.17f, "#d9a86a");
            slices = new GameObject("Slices").transform; slices.SetParent(board, false); for (int i = 0; i < 7; i++) Cyl(slices, 0.04f + i * 0.02f, 0.03f, 0.02f, 0.026f, 0.008f, "#e04848", new Vector3(0, 0, 90f)).SetActive(false);
            board.gameObject.SetActive(false);
            cupProp = Cyl(stage, CX + 0.1f, 0.565f, ZF + 0.14f, 0.032f, 0.07f, "#f4f1ea"); cupProp.SetActive(false);
            // shelf contents (decor): fridge and cupboard
            Cyl(fridge.pivot.parent, FX - 0.13f, 0.43f, ZF + 0.30f, 0.028f, 0.16f, "#7ac0e8"); Box(fridge.pivot.parent, FX + 0.09f, 0.42f, ZF + 0.30f, 0.09f, 0.14f, 0.07f, "#ffffff"); Ball(fridge.pivot.parent, FX + 0.15f, 0.72f, ZF + 0.28f, 0.07f, 0.07f, 0.07f, "#e04848");
            Ball(fridge.pivot.parent, FX - 0.1f, 0.72f, ZF + 0.28f, 0.07f, 0.07f, 0.07f, "#6bbf4a"); Box(fridge.pivot.parent, FX, 0.12f, ZF + 0.28f, 0.34f, 0.18f, 0.2f, "#ffd84a");
            for (int i = 0; i < 3; i++) Cyl(cabinet.pivot.parent, CX - 0.12f, 0.285f + i * 0.012f, ZF + 0.22f, 0.085f, 0.012f, i % 2 == 0 ? "#f4f1ea" : "#7ac0e8");
            Cyl(cabinet.pivot.parent, CX + 0.14f, 0.32f, ZF + 0.22f, 0.07f, 0.09f, "#aab3bd"); Cyl(cabinet.pivot.parent, CX + 0.14f, 0.075f, ZF + 0.22f, 0.1f, 0.03f, "#4a4f5c"); Cyl(cabinet.pivot.parent, CX - 0.12f, 0.06f, ZF + 0.2f, 0.07f, 0.06f, "#e88a8a");
            stage.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- items
        static bool Has(string s, params string[] k) { foreach (var x in k) if (s.Contains(x)) return true; return false; }
        GameObject MakeItem(string n)
        {
            var g = new GameObject("Item_" + n); var tr = g.transform; tr.SetParent(stage, false); n = (n ?? "").ToLowerInvariant();
            if (Has(n, "pan", "wok")) { Cyl(tr, 0, 0, 0, 0.08f, 0.03f, "#3a3d48"); Box(tr, 0.14f, 0.01f, 0, 0.12f, 0.016f, 0.026f, "#23252d"); }
            else if (Has(n, "pot", "kettle")) { Cyl(tr, 0, 0, 0, 0.065f, 0.08f, "#aab3bd"); Box(tr, 0.085f, 0.03f, 0, 0.05f, 0.012f, 0.016f, "#23252d"); Box(tr, -0.085f, 0.03f, 0, 0.05f, 0.012f, 0.016f, "#23252d"); }
            else if (Has(n, "plate", "dish")) { Cyl(tr, 0, 0, 0, 0.1f, 0.012f, "#f4f1ea"); Cyl(tr, 0, 0.007f, 0, 0.07f, 0.006f, "#7ac0e8"); }
            else if (Has(n, "bowl")) { Ball(tr, 0, 0, 0, 0.14f, 0.09f, 0.14f, "#f4f1ea"); Ball(tr, 0, 0.02f, 0, 0.115f, 0.06f, 0.115f, "#e8a45a"); }
            else if (Has(n, "cup", "glass", "mug")) { Cyl(tr, 0, 0, 0, 0.034f, 0.08f, "#f4f1ea"); Box(tr, 0.04f, 0, 0, 0.012f, 0.04f, 0.012f, "#f4f1ea"); }
            else if (Has(n, "bottle", "oil", "sauce", "milk", "juice", "water")) { Cyl(tr, 0, 0, 0, 0.03f, 0.15f, Has(n, "milk") ? "#ffffff" : "#7ac0e8"); Cyl(tr, 0, 0.09f, 0, 0.015f, 0.04f, "#e04848"); }
            else if (Has(n, "jar")) { Cyl(tr, 0, 0, 0, 0.04f, 0.09f, "#c9ecff"); Cyl(tr, 0, 0.05f, 0, 0.042f, 0.02f, "#e04848"); }
            else if (Has(n, "knife")) { Box(tr, 0, 0, 0.07f, 0.014f, 0.004f, 0.14f, "#d9dde3"); Box(tr, 0, 0, -0.03f, 0.02f, 0.02f, 0.09f, "#2b2d36"); }
            else if (Has(n, "spoon", "spatula", "fork", "ladle")) { Box(tr, 0, 0, 0, 0.012f, 0.012f, 0.16f, "#b98a5b"); Ball(tr, 0, 0, 0.1f, 0.05f, 0.016f, 0.07f, "#b98a5b"); }
            else if (Has(n, "board")) { Box(tr, 0, 0, 0, 0.2f, 0.014f, 0.13f, "#d9a86a"); }
            else if (Has(n, "egg")) { Ball(tr, 0, 0, 0, 0.045f, 0.06f, 0.045f, "#f6efe0"); Ball(tr, 0.05f, 0, 0, 0.045f, 0.06f, 0.045f, "#f6efe0"); }
            else if (Has(n, "cheese", "butter")) { Box(tr, 0, 0, 0, 0.1f, 0.05f, 0.07f, "#ffd84a"); }
            else if (Has(n, "tomato", "apple", "pepper")) { Ball(tr, 0, 0, 0, 0.08f, 0.08f, 0.08f, "#e04848"); Box(tr, 0, 0.045f, 0, 0.015f, 0.02f, 0.015f, "#4a8f3a"); }
            else if (Has(n, "onion", "potato")) { Ball(tr, 0, 0, 0, 0.085f, 0.075f, 0.085f, Has(n, "onion") ? "#c9a0e0" : "#c9a06a"); }
            else if (Has(n, "carrot")) { Cyl(tr, 0, 0, 0, 0.022f, 0.15f, "#ff8a1f", new Vector3(90, 0, 0)); }
            else if (Has(n, "box", "bag", "container")) { Box(tr, 0, 0, 0, 0.12f, 0.08f, 0.09f, "#e8a45a"); }
            else if (Has(n, "lid")) { Cyl(tr, 0, 0, 0, 0.08f, 0.01f, "#aab3bd"); Ball(tr, 0, 0.012f, 0, 0.025f, 0.02f, 0.025f, "#23252d"); }
            else { Ball(tr, 0, 0, 0, 0.08f, 0.08f, 0.08f, "#9a7ad9"); }
            return g;
        }

        // ---------------------------------------------------------------- start / stop
        static string Norm(string s) { return (s ?? "").ToLowerInvariant().Replace('_', ' '); }
        public void Begin(string v, string n)
        {
            string catNow = AppState.Category; if (catNow != "Kitchen") { BeginSport(catNow, v, n); return; }
            if (sportMode) { sport.Clear(); held = null; sportMode = false; Active = false; }
            if (!built) Build();
            string nv = Norm(v), nn = Norm(n); if (Active && nv == verb && nn == noun) return;
            verb = nv; noun = nn; t = 0f; lastSfxSeg = -1; prevChop = 0f;
            if (held != null) Destroy(held); held = null; if (boardVeg != null) Destroy(boardVeg); boardVeg = null; itemOn = false; itemTilt = 0f;
            if (!Active)
            {
                Active = true; stage.gameObject.SetActive(true);
                origin = girl.RepickSpot(true); Vector3 cam = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
                Vector3 cd = origin - cam; cd.y = 0; if (cd.sqrMagnitude < 0.01f) cd = Vector3.forward; cd.Normalize(); Vector3 camRight = Vector3.Cross(Vector3.up, cd);
                float lateral = Vector3.Dot(origin - cam, camRight); frameRot = Quaternion.LookRotation(Quaternion.Euler(0, lateral < 0 ? 62f : -62f, 0) * cd, Vector3.up);
                stage.SetPositionAndRotation(new Vector3(origin.x, girl.floorY, origin.z), frameRot);
            }
            fridge.angle = cabinet.angle = 0f; Apply(fridge); Apply(cabinet); flameOn = false; waterOn = false; pourOn = false; flames.gameObject.SetActive(false); water.gameObject.SetActive(false); board.gameObject.SetActive(false); potProp.SetActive(false); cupProp.SetActive(false);
            Pick();
        }
        void BeginSport(string cat, string v, string n)
        {
            if (Active && sport.Built && sportCat == cat && frameT != null && Camera.main != null)          // the stage follows you: put it back in front of you when you have walked away or turned
            {
                Vector3 cp = Camera.main.transform.position, cf = Camera.main.transform.forward; cf.y = 0; cf.Normalize(); Vector3 to = frameT.position - cp; to.y = 0; float dist = to.magnitude;
                if (dist < 1.8f || dist > 7.2f || Vector3.Angle(Quaternion.Euler(0, girl.StageYaw, 0) * cf, to) > 22f)
                {
                    Vector3 np = girl.StageSpot(); Vector3 cd2 = np - cp; cd2.y = 0; if (cd2.sqrMagnitude < 0.01f) cd2 = cf; cd2.Normalize(); float lat2 = Vector3.Dot(np - cp, Vector3.Cross(Vector3.up, cd2));
                    frameT.SetPositionAndRotation(new Vector3(np.x, girl.floorY, np.z), Quaternion.LookRotation(Quaternion.Euler(0, lat2 < 0 ? 40f : -40f, 0) * cd2, Vector3.up)); stand = frameT.position; faceDir = frameT.forward;
                }
            }
            if (!Active || sportCat != cat || !sport.Built)
            {
                Active = true; origin = girl.StageSpot(); Vector3 cam = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
                Vector3 cd = origin - cam; cd.y = 0; if (cd.sqrMagnitude < 0.01f) cd = Vector3.forward; cd.Normalize(); Vector3 camRight = Vector3.Cross(Vector3.up, cd); float lateral = Vector3.Dot(origin - cam, camRight);
                if (frameT == null) frameT = new GameObject("VeeFrame").transform; frameT.SetPositionAndRotation(new Vector3(origin.x, girl.floorY, origin.z), Quaternion.LookRotation(Quaternion.Euler(0, lateral < 0 ? 40f : -40f, 0) * cd, Vector3.up));
                if (built) stage.gameObject.SetActive(false); sport.Build(cat, frameT); sportCat = cat; stand = frameT.position; faceDir = frameT.forward;
            }
            sportMode = true; sport.Begin(v, n);
        }
        public void End()
        {
            if (!Active) return; Active = false; hasR = hasL = false; legMode = 0; direct = false; if (sportMode) { sport.Clear(); sportMode = false; sportCat = null; held = null; ReenactSfx.Loop("fire", false, Vector3.zero); girl.ReleasePlacement(); return; }
            if (held != null) Destroy(held); held = null; if (boardVeg != null) Destroy(boardVeg); boardVeg = null; stage.gameObject.SetActive(false); SfxOff(); girl.ReleasePlacement();
        }
        public void Idle() { hasR = hasL = false; legMode = 0; kind = Kind.None; faceCam = Mathf.Lerp(faceCam, 0f, 0.1f); }

        bool FridgeFood(string n) { return Has(n, "fridge", "freezer", "milk", "egg", "cheese", "butter", "juice", "yogurt", "tomato", "lettuce", "carrot", "drink", "food", "bottle", "jar", "sauce", "water", "apple", "vegetable", "fruit", "meat", "light"); }
        void Pick()
        {
            string v = verb, n = noun; kind = Kind.Show; closeFirst = false;
            if (v.StartsWith("open") || v.StartsWith("close") || v.StartsWith("shut")) { kind = Kind.OpenClose; closeFirst = !v.StartsWith("open"); curDoor = FridgeFood(n) && !Has(n, "cupboard", "drawer", "cabinet") ? fridge : cabinet; }
            else if (v.StartsWith("take") || v.StartsWith("get") || v.StartsWith("pick") || v.StartsWith("grab") || v.StartsWith("remove") || v.StartsWith("lift") || v.StartsWith("hold") || v.StartsWith("find") || v.StartsWith("search"))
            { kind = Kind.Take; curDoor = FridgeFood(n) ? fridge : (Has(n, "board", "knife", "plate", "bowl", "pot", "pan", "cup", "glass", "spoon", "fork", "lid", "utensil", "container", "box", "bag", "oil", "spice", "cupboard", "drawer", "cabinet") ? cabinet : null); if (Has(n, "fridge", "freezer")) { kind = Kind.OpenClose; curDoor = fridge; } if (Has(n, "cupboard", "drawer", "cabinet")) { kind = Kind.OpenClose; curDoor = cabinet; } }
            else if (v.StartsWith("put") || v.StartsWith("place") || v.StartsWith("move") || v.StartsWith("insert") || v.StartsWith("throw") || v.StartsWith("drop") || v.StartsWith("return")) kind = Kind.Put;
            else if (v.StartsWith("cut") || v.StartsWith("slice") || v.StartsWith("peel") || v.StartsWith("chop")) kind = Kind.Cut;
            else if (v.StartsWith("mix") || v.StartsWith("stir") || v.StartsWith("scoop") || v.StartsWith("spread") || v.StartsWith("knead") || v.StartsWith("cook") || v.StartsWith("fry") || v.StartsWith("heat")) kind = Kind.Stir;
            else if (v.StartsWith("wash") || v.StartsWith("rinse") || v.StartsWith("clean") || v.StartsWith("wipe") || v.StartsWith("dry") || v.StartsWith("scrub")) kind = Kind.Wash;
            else if (v.StartsWith("turn") || v.StartsWith("press") || v.StartsWith("adjust") || v.StartsWith("light") || v.StartsWith("switch")) kind = Kind.Knob;
            else if (v.StartsWith("pour") || v.StartsWith("fill") || v.StartsWith("shake") || v.StartsWith("empty")) kind = Kind.Pour;
            else if (v.StartsWith("drink") || v.StartsWith("eat") || v.StartsWith("taste")) kind = Kind.Drink;
            if (kind == Kind.Take && curDoor == null) curDoor = null;
            string itemName = noun; if (kind == Kind.Cut && !Has(itemName, "tomato", "onion", "potato", "carrot", "pepper", "apple")) itemName = "tomato";
            if (kind == Kind.Wash && itemName.Length == 0) itemName = "plate"; if (kind == Kind.Pour) itemName = Has(noun, "bottle", "oil", "sauce", "milk", "juice") ? noun : "bottle";
            if (kind == Kind.Stir || kind == Kind.Wash || kind == Kind.Cut || kind == Kind.Pour || kind == Kind.Take || kind == Kind.Put || kind == Kind.Show || kind == Kind.Drink)
            { if (kind == Kind.Stir) itemName = "spoon"; if (kind == Kind.Drink) itemName = "cup"; held = MakeItem(itemName); held.SetActive(kind == Kind.Cut || kind == Kind.Stir || kind == Kind.Pour || kind == Kind.Wash || kind == Kind.Drink || kind == Kind.Put); }
            if (kind == Kind.Cut) { board.gameObject.SetActive(true); boardVeg = MakeItem(itemName); boardVeg.transform.localPosition = board.localPosition + new Vector3(-0.04f, 0.05f, 0); boardVeg.transform.localRotation = Quaternion.identity; held = MakeItem("knife"); }
            if (kind == Kind.Stir) { potProp.SetActive(true); flameOn = true; }
            if (kind == Kind.Pour) cupProp.SetActive(true);
            if (kind == Kind.Take && curDoor != null) PlaceInside(held, curDoor);
            if (kind == Kind.Take && curDoor == null) { held.transform.localPosition = new Vector3(CX + 0.05f, 0.56f, ZF + 0.14f); held.SetActive(true); }
            standX = kind == Kind.OpenClose || kind == Kind.Take ? (curDoor != null ? curDoor.x : CX) : kind == Kind.Cut || kind == Kind.Pour || kind == Kind.Put || kind == Kind.Show ? CX : kind == Kind.Stir || kind == Kind.Knob ? SX : kind == Kind.Wash ? KX : CX;
            if (kind == Kind.Knob) flameOn = !v.Contains("off"); if (kind == Kind.Stir) flameOn = true;
        }
        void PlaceInside(GameObject item, Door d) { if (item == null) return; item.transform.localPosition = new Vector3(d.x, d == fridge ? 0.345f : 0.28f, ZF + 0.16f); item.transform.localRotation = Quaternion.identity; item.SetActive(true); }

        // ---------------------------------------------------------------- per-frame
        static float S(float t, float a, float b) { float k = Mathf.Clamp01((t - a) / (b - a)); return k * k * (3f - 2f * k); }
        Vector3 L(float x, float y, float z) { return stage.TransformPoint(new Vector3(x, y, z)); }
        Vector3 Rest(float x) { return L(x + 0.1f, 0.6f, 0.08f); }
        void Apply(Door d) { d.pivot.localRotation = Quaternion.Euler(0, d.angle, 0); }
        Vector3 Slot(Door d) { return L(d.x, (d == fridge ? 0.345f : 0.28f) + 0.04f, ZF + 0.16f); }
        Vector3 Chest(float x) { return L(x, 0.66f, 0.17f); }
        Vector3 ShowPt() { var g = girl.transform; return g.position + g.forward * 0.27f + Vector3.up * 0.8f; }
        void Sfx(string n, float vol = 1f) { ReenactSfx.Shot(n, stage.position + Vector3.up * 0.6f, vol); }
        void Seg(int i, string sfx) { if (i != lastSfxSeg) { lastSfxSeg = i; if (sfx != null) Sfx(sfx); } }

        void Update()
        {
            if (Active && sportMode) { sport.Tick(); return; }
            if (!Active || stage == null) return;
            t += Time.deltaTime; hasR = hasL = false; faceDir = stage.forward; stand = stage.position + stage.right * standX; stand.y = girl.floorY; itemOn = false;
            switch (kind)
            {
                case Kind.OpenClose: DoOpenClose(); break;
                case Kind.Take: DoTake(); break;
                case Kind.Put: DoPut(); break;
                case Kind.Cut: DoCut(); break;
                case Kind.Stir: DoStir(); break;
                case Kind.Wash: DoWash(); break;
                case Kind.Knob: DoKnob(); break;
                case Kind.Pour: DoPour(); break;
                case Kind.Drink: DoDrink(); break;
                default: DoShow(); break;
            }
            // flames / water / sounds
            flames.gameObject.SetActive(flameOn);
            if (flameOn) for (int i = 0; i < flameCones.Count; i++) flameCones[i].localScale = new Vector3(0.025f, 0.05f + 0.04f * Mathf.PerlinNoise(Time.time * 9f + i * 3.1f, i), 0.025f);
            water.gameObject.SetActive(waterOn || pourOn);
            ReenactSfx.Loop("fire", flameOn, stage.position + Vector3.up * 0.5f); ReenactSfx.Loop("sizzle", flameOn && kind == Kind.Stir, stage.position + Vector3.up * 0.5f); ReenactSfx.Loop("water", waterOn || pourOn, stage.position + Vector3.up * 0.5f);
        }
        void SfxOff() { ReenactSfx.Loop("fire", false, Vector3.zero); ReenactSfx.Loop("sizzle", false, Vector3.zero); ReenactSfx.Loop("water", false, Vector3.zero); }

        public void AfterPose()      // called by GuideGirl once the arms are posed: put the held item in her hand
        {
            if (!Active || held == null) return;
            if (!itemOn) return;
            held.SetActive(true); var hb = girl.HandBone(true); if (hb == null) return;
            held.transform.position = hb.position + girl.transform.forward * 0.03f + Vector3.down * 0.01f;
            held.transform.rotation = Quaternion.LookRotation(girl.transform.forward, Vector3.up) * Quaternion.Euler(itemTilt, 0, 0);
        }

        Vector3 Handle(Door d) { return d.handle.position + stage.forward * -0.02f; }

        void DoOpenClose()
        {
            float cyc = 6.4f; float tt = (t + (closeFirst ? 3.2f : 0f)) % cyc; var d = curDoor;
            d.angle = tt < 0.8f ? 0f : tt < 2.0f ? 100f * S(tt, 0.8f, 2.0f) : tt < 4.0f ? 100f : tt < 5.2f ? 100f * (1f - S(tt, 4.0f, 5.2f)) : 0f; Apply(d);
            float w = tt < 2.0f ? S(tt, 0f, 0.8f) : tt < 3.4f ? 1f - S(tt, 2.0f, 2.8f) : tt < 4.0f ? S(tt, 3.4f, 4.0f) : tt < 5.2f ? 1f : 1f - S(tt, 5.2f, 6.0f);
            tR = Vector3.Lerp(Rest(d.x), Handle(d), w); hasR = true; faceCam = 0f;
            int seg = tt < 2.0f ? 0 : tt < 4.0f ? 1 : 2; if (seg != lastSfxSeg) { lastSfxSeg = seg; if (seg == 0 && tt >= 0.7f) Sfx("door"); else if (seg == 2) Sfx("door"); }
            if (tt >= 0.8f && tt < 0.9f) Seg(10, "door");
        }

        void DoTake()
        {
            var d = curDoor; bool door = d != null; float cyc = door ? 9.6f : 6.0f; float tt = t % cyc; faceCam = 0f;
            float x = door ? d.x : CX; Vector3 slot = door ? Slot(d) : L(CX + 0.05f, 0.62f, ZF + 0.14f); Vector3 chest = Chest(x);
            if (door) d.angle = tt < 0.7f ? 0f : tt < 1.7f ? 100f * S(tt, 0.7f, 1.7f) : tt < 8.0f ? 100f : tt < 9.0f ? 100f * (1f - S(tt, 8.0f, 9.0f)) : 0f; if (door) Apply(d);
            Vector3 hp;
            if (door)
            {
                Vector3 handle = Handle(d);
                if (tt < 0.7f) hp = Vector3.Lerp(Rest(x), handle, S(tt, 0f, 0.7f));
                else if (tt < 1.7f) hp = handle;
                else if (tt < 2.5f) hp = Vector3.Lerp(handle, slot, S(tt, 1.7f, 2.5f));
                else if (tt < 2.9f) hp = slot;
                else if (tt < 3.8f) hp = Vector3.Lerp(slot, chest, S(tt, 2.9f, 3.8f));
                else if (tt < 4.4f) hp = Vector3.Lerp(chest, ShowPt(), S(tt, 3.8f, 4.4f));
                else if (tt < 6.0f) hp = ShowPt() + Vector3.up * Mathf.Sin(tt * 3f) * 0.01f;
                else if (tt < 6.8f) hp = Vector3.Lerp(ShowPt(), slot, S(tt, 6.0f, 6.8f));
                else if (tt < 7.4f) hp = Vector3.Lerp(slot, Rest(x), S(tt, 6.8f, 7.4f));
                else if (tt < 8.0f) hp = Vector3.Lerp(Rest(x), handle, S(tt, 7.4f, 8.0f));
                else if (tt < 9.0f) hp = handle; else hp = Vector3.Lerp(handle, Rest(x), S(tt, 9.0f, 9.6f));
                itemOn = tt >= 2.9f && tt < 6.8f; if (!itemOn && held != null) PlaceInside(held, d);
                faceCam = tt > 3.8f && tt < 6.0f ? S(tt, 3.8f, 4.5f) : tt >= 6.0f && tt < 6.8f ? 1f - S(tt, 6.0f, 6.8f) : 0f;
                if (tt >= 0.7f && tt < 1.0f) Seg(1, "door"); else if (tt >= 8.0f && tt < 8.3f) Seg(2, "door"); else if (tt >= 2.9f && tt < 3.0f) Seg(3, "clink"); else if (tt < 0.1f) lastSfxSeg = -1;
            }
            else
            {
                if (tt < 0.8f) hp = Vector3.Lerp(Rest(x), slot, S(tt, 0f, 0.8f)); else if (tt < 1.1f) hp = slot; else if (tt < 1.9f) hp = Vector3.Lerp(slot, chest, S(tt, 1.1f, 1.9f));
                else if (tt < 2.5f) hp = Vector3.Lerp(chest, ShowPt(), S(tt, 1.9f, 2.5f)); else if (tt < 4.2f) hp = ShowPt() + Vector3.up * Mathf.Sin(tt * 3f) * 0.01f;
                else if (tt < 5.0f) hp = Vector3.Lerp(ShowPt(), slot, S(tt, 4.2f, 5.0f)); else hp = Vector3.Lerp(slot, Rest(x), S(tt, 5.0f, 5.7f));
                itemOn = tt >= 1.1f && tt < 5.0f; if (!itemOn && held != null) { held.transform.localPosition = new Vector3(CX + 0.05f, 0.56f, ZF + 0.14f); held.SetActive(true); }
                faceCam = tt > 1.9f && tt < 4.2f ? S(tt, 1.9f, 2.6f) : tt >= 4.2f && tt < 5.0f ? 1f - S(tt, 4.2f, 5.0f) : 0f;
            }
            tR = hp; hasR = true;
        }

        void DoPut()
        {
            float tt = t % 5f; float x = CX; Vector3 spot = L(CX + 0.0f, 0.57f, ZF + 0.15f); faceCam = 0f;
            Vector3 hp = tt < 0.5f ? Chest(x) : tt < 1.5f ? Vector3.Lerp(Chest(x), spot + Vector3.up * 0.05f, S(tt, 0.5f, 1.5f)) : tt < 2.3f ? Vector3.Lerp(spot + Vector3.up * 0.05f, Rest(x), S(tt, 1.5f, 2.3f)) : Rest(x);
            tR = hp; hasR = true; bool inHand = tt < 1.5f; itemOn = inHand;
            if (!inHand && held != null) { held.transform.position = spot; held.transform.rotation = stage.rotation; held.SetActive(true); }
            if (tt >= 1.5f && tt < 1.6f) Seg(4, "clink"); else if (tt < 0.1f) lastSfxSeg = -1;
        }

        void DoCut()
        {
            float tt = t; Vector3 c = board.position + Vector3.up * 0.0f; Vector3 right = stage.right;
            float s = Mathf.Sin(tt * 7f); float lift = Mathf.Max(0f, s); float slide = Mathf.Repeat(tt * 0.18f, 0.9f);
            tR = c + right * (0.02f + 0.05f * slide) + Vector3.up * (0.085f + 0.07f * lift) + stage.forward * 0.0f; hasR = true; itemOn = true; itemTilt = 70f;
            tL = c + right * (-0.07f) + Vector3.up * 0.075f + stage.forward * 0.0f; hasL = true; faceCam = 0f;
            if (prevChop > 0f && s <= 0f) Sfx("chop", 0.9f); prevChop = s;
            int n = Mathf.Min(7, Mathf.FloorToInt(tt * 1.6f) % 9); for (int i = 0; i < slices.childCount; i++) slices.GetChild(i).gameObject.SetActive(i < n);
            if (boardVeg != null) boardVeg.transform.localScale = new Vector3(Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(n / 7f)), 1f, 1f);
        }

        void DoStir()
        {
            Vector3 c = potProp.transform.position; float a = t * 5f; faceCam = 0f;
            tR = c + new Vector3(0, 0.1f, 0) + stage.right * (Mathf.Cos(a) * 0.035f) + stage.forward * (Mathf.Sin(a) * 0.035f); hasR = true; itemOn = true; itemTilt = 30f;
            tL = c + stage.right * -0.13f + Vector3.up * 0.06f; hasL = true; flameOn = true;
        }

        void DoWash()
        {
            waterOn = true; faceCam = 0f; Vector3 c = L(KX, 0.62f, ZF + 0.2f); float s = Mathf.Sin(t * 5f);
            tL = c + stage.right * -0.05f; hasL = true; tR = c + stage.right * (0.04f + s * 0.04f) + Vector3.up * 0.02f; hasR = true; itemOn = false;
            if (held != null) { held.transform.position = c + Vector3.up * 0.02f; held.transform.rotation = stage.rotation; held.SetActive(true); }
        }

        void DoKnob()
        {
            float cyc = 4.6f; float tt = t % cyc; bool on = !verb.Contains("off"); faceCam = 0f; Vector3 kp = knob.position + stage.forward * -0.03f;
            tR = tt < 0.8f ? Vector3.Lerp(Rest(SX), kp, S(tt, 0f, 0.8f)) : tt < 3.2f ? kp : Vector3.Lerp(kp, Rest(SX), S(tt, 3.2f, 4.0f)); hasR = true;
            float rot = S(tt, 0.8f, 1.6f) * (on ? 120f : -120f) * (tt < 3.2f ? 1f : 1f); knob.localRotation = Quaternion.Euler(90, 0, on ? rot : 120f + rot);
            bool lit = on ? tt > 1.2f : tt < 1.2f; flameOn = lit; potProp.SetActive(true);
            if (tt >= 0.8f && tt < 0.95f) Seg(5, "clink"); else if (tt < 0.1f) lastSfxSeg = -1;
        }

        void DoPour()
        {
            float cyc = 5.0f; float tt = t % cyc; Vector3 cup = cupProp.transform.position; faceCam = 0f;
            Vector3 above = cup + new Vector3(0, 0.13f, 0) + stage.right * -0.05f;
            tR = tt < 0.8f ? Vector3.Lerp(Rest(CX), above, S(tt, 0f, 0.8f)) : tt < 3.6f ? above : Vector3.Lerp(above, Rest(CX), S(tt, 3.6f, 4.4f)); hasR = true; itemOn = true;
            itemTilt = tt < 0.8f ? 0f : tt < 1.4f ? 100f * S(tt, 0.8f, 1.4f) : tt < 3.2f ? 100f : 100f * (1f - S(tt, 3.2f, 3.8f));
            pourOn = tt > 1.2f && tt < 3.3f; if (pourOn) { water.position = cup + Vector3.up * 0.1f; water.localScale = new Vector3(0.018f, 0.075f, 0.018f); water.rotation = Quaternion.identity; }
        }

        void DoDrink()
        {
            float tt = t % 5f; Vector3 mouth = girl.transform.position + girl.transform.forward * 0.1f + Vector3.up * 0.93f; faceCam = 0f;
            tR = tt < 0.8f ? Vector3.Lerp(Rest(CX), mouth, S(tt, 0f, 0.8f)) : tt < 3.2f ? mouth : Vector3.Lerp(mouth, Rest(CX), S(tt, 3.2f, 4.0f)); hasR = true; itemOn = true; itemTilt = tt > 1f && tt < 3f ? -40f : 0f;
        }

        void DoShow()
        {
            float tt = t % 5f; Vector3 hand = ShowPt(); tR = tt < 0.8f ? Vector3.Lerp(Rest(CX), hand, S(tt, 0f, 0.8f)) : tt < 3.8f ? hand + Vector3.up * Mathf.Sin(tt * 3f) * 0.01f : Vector3.Lerp(hand, Rest(CX), S(tt, 3.8f, 4.6f)); hasR = true; itemOn = true;
            faceCam = tt > 0.4f && tt < 4f ? S(tt, 0.4f, 1.1f) : 0f;
        }
    }

    /// <summary>Procedurally generated kitchen sounds (no audio files): chop, door, clink, and loops for fire, sizzle and running water.</summary>
    public static class ReenactSfx
    {
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>(); static GameObject host; static AudioSource oneShot; static readonly Dictionary<string, AudioSource> loops = new Dictionary<string, AudioSource>();
        const int SR = 22050;

        static AudioClip Make(string name)
        {
            if (clips.TryGetValue(name, out var c)) return c;
            var rng = new System.Random(name.GetHashCode()); float[] d;
            switch (name)
            {
                case "chop": { int n = (int)(SR * 0.16f); d = new float[n]; float lp = 0; for (int i = 0; i < n; i++) { float t = i / (float)SR; float nz = (float)(rng.NextDouble() * 2 - 1); lp += 0.5f * (nz - lp); d[i] = (nz - lp) * Mathf.Exp(-t * 55f) * 0.8f + Mathf.Sin(2 * Mathf.PI * 150f * t) * Mathf.Exp(-t * 35f) * 0.9f; } break; }
                case "clink": { int n = (int)(SR * 0.35f); d = new float[n]; for (int i = 0; i < n; i++) { float t = i / (float)SR; d[i] = (Mathf.Sin(2 * Mathf.PI * 2400f * t) + 0.6f * Mathf.Sin(2 * Mathf.PI * 3700f * t)) * Mathf.Exp(-t * 14f) * 0.45f; } break; }
                case "door": { int n = (int)(SR * 0.7f); d = new float[n]; float ph = 0; for (int i = 0; i < n; i++) { float t = i / (float)SR; float f = 190f + 90f * Mathf.Sin(t * 5f) + 120f * t; ph += 2 * Mathf.PI * f / SR; float env = Mathf.Clamp01(t / 0.05f) * Mathf.Clamp01((0.7f - t) / 0.18f); float nz = (float)(rng.NextDouble() * 2 - 1); d[i] = (Mathf.Sin(ph) * 0.35f + nz * 0.08f) * env; } break; }
                case "fire": { int n = SR * 2; d = new float[n]; float lp = 0, lp2 = 0; for (int i = 0; i < n; i++) { float nz = (float)(rng.NextDouble() * 2 - 1); lp += 0.03f * (nz - lp); lp2 += 0.25f * (nz - lp2); float pop = rng.NextDouble() < 0.0018 ? (float)(rng.NextDouble() * 0.9) : 0f; d[i] = lp * 1.6f + (nz - lp2) * 0.05f + pop * Mathf.Sin(i * 0.9f) * 0.6f; } Fade(d); break; }
                case "sizzle": { int n = SR * 2; d = new float[n]; float lp = 0; for (int i = 0; i < n; i++) { float nz = (float)(rng.NextDouble() * 2 - 1); lp += 0.6f * (nz - lp); float crack = rng.NextDouble() < 0.004 ? 0.8f : 0f; d[i] = (nz - lp) * 0.22f * (0.6f + 0.4f * Mathf.Sin(i * 0.0017f)) + crack * (float)(rng.NextDouble() * 2 - 1); } Fade(d); break; }
                default: { int n = SR * 2; d = new float[n]; float lp = 0, lp2 = 0; for (int i = 0; i < n; i++) { float nz = (float)(rng.NextDouble() * 2 - 1); lp += 0.18f * (nz - lp); lp2 += 0.04f * (lp - lp2); d[i] = (lp - lp2) * 0.9f * (0.75f + 0.25f * Mathf.Sin(i * 0.002f)); } Fade(d); break; }       // "water"
            }
            float mx = 0.001f; foreach (var v in d) mx = Mathf.Max(mx, Mathf.Abs(v)); for (int i = 0; i < d.Length; i++) d[i] = d[i] / mx * 0.7f;
            var clip = AudioClip.Create("sfx_" + name, d.Length, 1, SR, false); clip.SetData(d, 0); clips[name] = clip; return clip;
        }
        static void Fade(float[] d) { int f = SR / 20; for (int i = 0; i < f; i++) { float k = i / (float)f; d[i] *= k; d[d.Length - 1 - i] *= k; } }
        static void Host() { if (host == null) { host = new GameObject("ReenactSfx"); UnityEngine.Object.DontDestroyOnLoad(host); oneShot = host.AddComponent<AudioSource>(); oneShot.spatialBlend = 0.8f; oneShot.playOnAwake = false; } }
        public static void Shot(string name, Vector3 pos, float vol = 1f) { Host(); host.transform.position = pos; oneShot.PlayOneShot(Make(name), 0.9f * vol); }
        public static void Loop(string name, bool on, Vector3 pos)
        {
            Host(); if (!loops.TryGetValue(name, out var a)) { if (!on) return; var go = new GameObject("loop_" + name); go.transform.SetParent(host.transform, false); a = go.AddComponent<AudioSource>(); a.clip = Make(name); a.loop = true; a.spatialBlend = 0.8f; a.volume = 0f; a.playOnAwake = false; loops[name] = a; }
            if (on) { a.transform.position = pos; if (!a.isPlaying) a.Play(); }
            float target = on ? (name == "fire" ? 0.55f : name == "sizzle" ? 0.5f : 0.45f) : 0f; a.volume = Mathf.MoveTowards(a.volume, target, Time.deltaTime * 2.5f); if (!on && a.volume <= 0.001f && a.isPlaying) a.Stop();
        }
    }
}
