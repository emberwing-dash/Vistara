using System.Collections.Generic;
using UnityEngine;

namespace Vistara
{
    /// <summary>
    /// For every non-kitchen category: all of that category's props (the same Blender models as the world, from assets/categories/&lt;cat&gt;/export) are put on a small stage next to Vee and she really uses them:
    /// she rides the bike / motorbike / horse, stands on and rides the skateboard / longboard / surfboard / skis, boxes the punching bag, climbs the wall with the holds, vaults the boxes and rails,
    /// picks up and shows small objects (gloves, mallet, ball, helmet, rope ...), flies the plane, swims among the fish.
    /// </summary>
    public class SportStage
    {
        enum Role { Decor, Ride, RideStand, Hit, Climb, Hop, Hold, Fly, Swim }
        class Item
        {
            public string name; public Role role; public Transform tr; public Vector3 slot; public Quaternion slotRot; public float s = 1f; public Vector3 size; public Vector3 fwdLocal = Vector3.forward;
            public float rear, front; public float seatT = -1f; public float seatTop;
            public List<Vector3> verts = new List<Vector3>(); public Vector3 ctr; public float lenAxis; public bool axisX;
        }
        static readonly Dictionary<string, string[]> Cfg = new Dictionary<string, string[]>
        {
            { "Bike", new[] { "Bike:Ride" } },
            { "BikePolo", new[] { "Bike:Ride", "Mallet:Hold", "Ball:Hold", "Goal:Decor", "Cone:Decor" } },
            { "Boxing", new[] { "PunchingBag:Hit", "SpeedBag:Hit", "Glove:Hold", "Dumbbell:Hold", "Kettlebell:Hold", "Bench:Decor", "Rope:Hold" } },
            { "Horse", new[] { "Horse:Ride", "HorseWhite:Decor", "Foal:Decor", "Hay:Decor", "Bucket:Hold", "Trough:Decor", "Jump:Hop" } },
            { "Jumping", new[] { "Box:Hop", "Crate:Hop", "Ramp:Hop", "Rail:Hop", "AC:Decor", "WaterTank:Decor" } },
            { "Longboarding", new[] { "Board:RideStand", "Cone:Decor" } },
            { "Motor", new[] { "Bike:Ride", "BikeBlue:Decor", "Cone:Decor", "TyreStack:Hop" } },
            { "Parkour", new[] { "Box:Hop", "LowWall:Hop", "Rail:Hop", "Stairs:Hop", "Bench:Hop", "Bike:Ride", "Wall:Climb", "BikeRack:Decor" } },
            { "Plane", new[] { "Plane:Fly", "PlaneBlue:Decor", "Balloon:Decor", "Ring:Decor" } },
            { "RockClimbing", new[] { "CliffBlock:Climb", "Hold:Decor", "HoldBlue:Decor", "HoldYellow:Decor", "HoldGreen:Decor", "Rope:Hold", "Carabiner:Hold", "Helmet:Hold", "Backpack:Hold", "Tent:Decor" } },
            { "Scuba", new[] { "Fish:Swim", "FishBlue:Swim", "FishYellow:Swim", "FishPink:Swim", "Shark:Swim", "Turtle:Swim", "Jellyfish:Swim", "Chest:Hold", "Anchor:Decor", "Starfish:Hold", "CoralPink:Decor", "CoralOrange:Decor", "CoralPurple:Decor", "BrainCoral:Decor" } },
            { "Skate", new[] { "Skateboard:RideStand", "SkateboardBlue:Decor", "QuarterPipe:Hop", "Funbox:Hop", "Rail:Hop", "Ledge:Hop", "Stairs:Hop", "Bowl:Decor", "Cone:Decor" } },
            { "Ski", new[] { "Skis:RideStand", "Poles:Hold", "Flag:Decor", "Snowman:Decor" } },
            { "Surfing", new[] { "Surfboard:RideStand", "SurfboardBlue:Decor", "SurfboardPink:Decor", "Umbrella:Decor", "Towel:Hold", "Crab:Decor", "Sandcastle:Decor", "Buoy:Decor" } },
        };
        static readonly Dictionary<string, string> Primary = new Dictionary<string, string> {
            { "Bike", "Bike" }, { "BikePolo", "Bike" }, { "Boxing", "PunchingBag" }, { "Horse", "Horse" }, { "Jumping", "Box" }, { "Longboarding", "Board" }, { "Motor", "Bike" }, { "Parkour", "Box" },
            { "Plane", "Plane" }, { "RockClimbing", "CliffBlock" }, { "Scuba", "Fish" }, { "Skate", "Skateboard" }, { "Ski", "Skis" }, { "Surfing", "Surfboard" } };

        readonly Reenactor R; readonly GuideGirl G; Transform root; string cat; readonly List<Item> items = new List<Item>(); Item cur; Transform frame; float t; string verb = "", noun = "";
        float prevHit; Item carried; Vector3 carryBase; float pathT, legT;

        public SportStage(Reenactor r, GuideGirl g) { R = r; G = g; }

        static Role ParseRole(string r) { System.Enum.TryParse(r, out Role x); return x; }
        static Color Col(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
        static Color ColorOf(string matName) { var m = System.Text.RegularExpressions.Regex.Match(matName ?? "", "M_([0-9a-fA-F]{6})"); return m.Success ? Col("#" + m.Groups[1].Value) : new Color(0.75f, 0.75f, 0.78f); }

        // ---------------------------------------------------------------- build
        public void Build(string category, Transform stageFrame)
        {
            Clear(); cat = category; frame = stageFrame; root = new GameObject("VeeProps_" + cat).transform; root.SetParent(null);
            if (!Cfg.TryGetValue(cat, out var list)) return;
            int n = list.Length; int idx = 0;
            foreach (var entry in list)
            {
                var parts = entry.Split(':'); var prefab = Resources.Load<GameObject>("Props/" + cat + "_" + parts[0]); if (prefab == null) { Debug.LogWarning("[Vee] missing prop " + cat + "_" + parts[0]); continue; }
                var it = new Item { name = parts[0], role = ParseRole(parts[1]) };
                var wrap = new GameObject(it.name).transform; wrap.SetParent(root, false); it.tr = wrap;
                var inst = Object.Instantiate(prefab, wrap); inst.transform.localPosition = Vector3.zero; inst.transform.localRotation = Quaternion.identity; inst.transform.localScale = Vector3.one;
                var rends = inst.GetComponentsInChildren<Renderer>(); Bounds b = new Bounds(); bool first = true;
                foreach (var r in rends) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); var ms = r.sharedMaterials; for (int i = 0; i < ms.Length; i++) ms[i] = G.Mat(ColorOf(ms[i] != null ? ms[i].name : "")); r.sharedMaterials = ms; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
                Vector3 off = new Vector3(-b.center.x, -b.min.y, -b.center.z); inst.transform.localPosition = off;
                foreach (var mf in inst.GetComponentsInChildren<MeshFilter>()) if (mf.sharedMesh != null)
                {
                    var m = mf.transform.localToWorldMatrix;
                    if (mf.sharedMesh.isReadable) { foreach (var v in mf.sharedMesh.vertices) it.verts.Add(wrap.InverseTransformPoint(m.MultiplyPoint3x4(v))); }
                    else { var mb = mf.sharedMesh.bounds; for (int cx = 0; cx < 8; cx++) it.verts.Add(wrap.InverseTransformPoint(m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((cx & 1) * 2 - 1, ((cx >> 1) & 1) * 2 - 1, ((cx >> 2) & 1) * 2 - 1))))); }          // not readable: its bounding box corners
                }
                it.size = b.size; float maxD = Mathf.Max(b.size.x, b.size.z, 0.01f); float s = Mathf.Min(0.62f, 1.7f / maxD); s = Mathf.Max(s, 0.24f / Mathf.Max(maxD, b.size.y)); if (it.role == Role.Hit) s = (it.name == "SpeedBag" ? 0.55f : 1.05f) / Mathf.Max(0.05f, b.size.y);          // a heavy bag about as tall as her; the speed bag at her face
                if (it.role == Role.Swim) s = Mathf.Min(0.6f, 0.5f / maxD);
                it.s = s; wrap.localScale = Vector3.one * s;
                Orient(it); items.Add(it); idx++;
            }
            var camT = Camera.main != null ? Camera.main.transform : null; float towards = camT != null ? Vector3.Dot(frame.right, Flat(camT.position - frame.position).normalized) : 0f;
            float a0 = towards > 0.15f ? -85f : (towards < -0.15f ? -10f : -60f), a1 = towards > 0.15f ? 10f : (towards < -0.15f ? 85f : 60f);          // keep the props on the far side so they never hide her
            for (int i = 0; i < items.Count; i++)
            {
                float a = Mathf.Lerp(a0, a1, items.Count == 1 ? 0.5f : i / (float)(items.Count - 1)) * Mathf.Deg2Rad; float rad = items.Count > 8 ? 1.9f : 1.45f; if (i % 2 == 1 && items.Count > 8) rad += 0.45f;
                var it = items[i]; it.slot = frame.TransformPoint(new Vector3(Mathf.Sin(a) * rad, 0, Mathf.Cos(a) * rad * 0.8f + 0.2f)); it.slotRot = Quaternion.LookRotation(frame.position - it.slot) * Quaternion.Inverse(Quaternion.LookRotation(it.fwdLocal)); it.tr.SetPositionAndRotation(it.slot, it.slotRot);
            }
            // rock-climbing holds go on the face of the climbing block
            var cliff = items.Find(x => x.role == Role.Climb); if (cliff != null && cat == "RockClimbing") PlaceHolds(cliff);
        }
        void Orient(Item it)
        {
            if (it.verts.Count == 0) return; Vector3 mn = it.verts[0], mx = it.verts[0]; foreach (var v in it.verts) { mn = Vector3.Min(mn, v); mx = Vector3.Max(mx, v); }
            it.axisX = (mx.x - mn.x) > (mx.z - mn.z); it.ctr = (mn + mx) * 0.5f; float lo = it.axisX ? mn.x : mn.z, hi = it.axisX ? mx.x : mx.z; it.lenAxis = hi - lo; it.rear = lo; it.front = hi;
            float topHi = 0, topLo = 0; foreach (var v in it.verts) { float p = it.axisX ? v.x : v.z; if (p > lo + 0.75f * (hi - lo)) topHi = Mathf.Max(topHi, v.y); if (p < lo + 0.25f * (hi - lo)) topLo = Mathf.Max(topLo, v.y); }
            bool frontIsHi = topHi >= topLo; if (!frontIsHi) { float t0 = it.rear; it.rear = it.front; it.front = t0; }
            Vector3 axis = it.axisX ? Vector3.right : Vector3.forward; it.fwdLocal = frontIsHi ? axis : -axis;
        }
        Vector3 LP(Item it, float t01, float lat, out float top)        // local point along the long axis (t01: 0 rear .. 1 front), lateral offset, and the top height there
        {
            float p = Mathf.Lerp(it.rear, it.front, t01); top = 0f; float win = Mathf.Max(0.12f, it.lenAxis * 0.06f);
            foreach (var v in it.verts) { float q = it.axisX ? v.x : v.z; if (Mathf.Abs(q - p) < win) top = Mathf.Max(top, v.y); }
            Vector3 lat3 = it.axisX ? Vector3.forward : Vector3.right; Vector3 pos = (it.axisX ? Vector3.right : Vector3.forward) * p + lat3 * lat + it.ctr.y * Vector3.zero; pos.y = top; return pos;
        }
        void PlaceHolds(Item cliff)
        {
            Vector3 toward = (frame.position - cliff.tr.position); toward.y = 0; toward.Normalize(); var bnd = WorldBounds(cliff); int i = 0;
            foreach (var h in items) if (h.name.StartsWith("Hold"))
                {
                    float lat = (i % 3 - 1) * 0.28f, y = 0.22f + 0.2f * i; Vector3 side = Vector3.Cross(Vector3.up, toward); Vector3 target = bnd.center + toward * 10f + side * lat; target.y = y + bnd.min.y;
                    Vector3 surf = bnd.ClosestPoint(target); surf.y = target.y; h.tr.position = surf + toward * 0.02f; h.tr.rotation = Quaternion.LookRotation(-toward); i++;
                }
        }
        Bounds WorldBounds(Item it) { var rs = it.tr.GetComponentsInChildren<Renderer>(); Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b; }

        public void Clear() { if (root != null) Object.Destroy(root.gameObject); root = null; items.Clear(); cur = null; carried = null; }
        public bool Built { get { return root != null && items.Count > 0; } }
        public string Cat { get { return cat; } }

        // ---------------------------------------------------------------- choose + run
        static bool Has(string s, params string[] k) { foreach (var x in k) if (s.Contains(x)) return true; return false; }
        public void Begin(string v, string n)
        {
            verb = (v ?? "").ToLowerInvariant(); noun = (n ?? "").ToLowerInvariant().Replace('_', ' '); t = 0f; prevHit = 0f; ResetItems();
            Item pick = null; foreach (var it in items) if (it.role != Role.Decor && noun.Length > 2 && noun.Contains(it.name.ToLowerInvariant())) { pick = it; break; }
            if (pick == null) foreach (var it in items) { string a = it.name.ToLowerInvariant(); if (noun.Length > 2 && ((Has(noun, "bicycle", "cycle") && a == "bike") || (Has(noun, "skate") && a.StartsWith("skateboard")) || (Has(noun, "surf") && a.StartsWith("surfboard")) || (Has(noun, "ski") && a == "skis") || (Has(noun, "punch", "bag") && a == "punchingbag") || (Has(noun, "wall") && it.role == Role.Climb) || (Has(noun, "box", "crate", "rail", "ledge", "stairs", "bench", "ramp") && it.role == Role.Hop && a.Contains(noun.Split(' ')[0])))) { pick = it; break; } }
            if (pick == null && Has(verb, "hit", "punch", "strike", "kick")) pick = items.Find(x => x.role == Role.Hit);
            if (pick == null && Has(verb, "jump", "hop", "vault", "leap", "land")) pick = items.Find(x => x.role == Role.Hop);
            if (pick == null && Has(verb, "climb", "reach", "hang")) pick = items.Find(x => x.role == Role.Climb);
            if (pick == null && Has(verb, "ride", "cycl", "pedal", "drive", "skate", "surf", "ski", "glide", "cruise")) pick = items.Find(x => x.role == Role.Ride || x.role == Role.RideStand);
            if (pick == null && Has(verb, "fly")) pick = items.Find(x => x.role == Role.Fly);
            if (pick == null && Has(verb, "swim", "dive")) pick = items.Find(x => x.role == Role.Swim);
            if (pick == null && Has(verb, "take", "pick", "grab", "hold", "get", "put", "carry", "lift")) pick = items.Find(x => x.role == Role.Hold);
            if (pick == null && Primary.TryGetValue(cat, out var pn)) pick = items.Find(x => x.name == pn);
            if (pick == null && items.Count > 0) pick = items.Find(x => x.role != Role.Decor) ?? items[0];
            cur = pick; R.legMode = 0; carried = null; R.Carry(null, false, 0f);
        }
        void ResetItems() { foreach (var it in items) { it.tr.SetPositionAndRotation(it.slot, it.slotRot); } if (cat == "RockClimbing") { var c = items.Find(x => x.role == Role.Climb); if (c != null) PlaceHolds(c); } R.direct = false; }

        Vector3 Flat(Vector3 v) { v.y = 0; return v; }
        void Hands(Vector3 a, Vector3 b)          // give each hand the target closest to it
        {
            var hr = G.HandBone(true); bool swap = hr != null && Vector3.Distance(hr.position, b) < Vector3.Distance(hr.position, a);
            R.tR = swap ? b : a; R.tL = swap ? a : b; R.hasR = R.hasL = true;
        }

        public void Tick()
        {
            t += Time.deltaTime * R.Tempo; R.hasR = R.hasL = false; R.legMode = 0; R.faceCam = 0f; R.direct = false; Ambient();
            if (cur == null) return;
            switch (cur.role)
            {
                case Role.Ride: Ride(cur, false); break;
                case Role.RideStand: Ride(cur, true); break;
                case Role.Hit: Hit(cur); break;
                case Role.Climb: Climb(cur); break;
                case Role.Hop: Hop(cur); break;
                case Role.Hold: Hold(cur); break;
                case Role.Fly: Fly(cur); break;
                case Role.Swim: Swim(cur); break;
                default: Hold(cur); break;
            }
        }

        void Ambient()
        {
            int k = 0; foreach (var it in items)
            {
                k++; if (it == cur && it.role != Role.Swim) continue;
                if (it.role == Role.Swim) { float a = Time.time * (0.25f + 0.07f * (k % 5)) + k; float r = 0.9f + 0.12f * (k % 4); Vector3 p = frame.position + frame.TransformDirection(new Vector3(Mathf.Cos(a) * r, 0.35f + 0.1f * (k % 5) + 0.05f * Mathf.Sin(a * 3f), Mathf.Sin(a) * r * 0.8f + 0.9f)); Vector3 tan = frame.TransformDirection(new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a) * 0.8f)); it.tr.position = p; it.tr.rotation = Quaternion.LookRotation(tan) * Quaternion.Inverse(Quaternion.LookRotation(it.fwdLocal)); }
                else if (it.name == "Balloon") it.tr.position = it.slot + Vector3.up * (0.1f * Mathf.Sin(Time.time * 1.3f + k));
                else if (it.name.StartsWith("Plane") && it.role == Role.Decor) it.tr.rotation = it.slotRot * Quaternion.Euler(0, Time.time * 20f, 0);
            }
        }

        // ---- ride (sitting: bike/motor/horse; standing: boards/skis)
        void Ride(Item it, bool stand)
        {
            // she acts what the video shows right now: moving, going fast, turning, stopped, jumping
            string mode = R.Mode; float sp = mode == "stop" || mode == "idle" ? 0.3f : mode == "fast" ? 1.7f : mode == "turn" ? 0.85f : 1f;
            pathT += Time.deltaTime * sp * (cat == "Horse" ? 0.38f : 0.55f); legT += Time.deltaTime * sp * (cat == "Horse" ? 3f : 6f);
            float ang = pathT; float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
            Vector3 pos = frame.position + frame.right * (ca * 1.8f) + frame.forward * (sa * 0.6f); Vector3 tan = (frame.right * (-sa * 1.8f) + frame.forward * (ca * 0.6f)).normalized;          // she rides forward across your view along a wide oval, like in the video
            float bob = stand && cat == "Surfing" ? 0.03f * Mathf.Sin(t * 3f) : (!stand ? 0.012f * Mathf.Sin(legT) : 0f);
            if (mode == "jump") bob += 0.28f * Mathf.Abs(Mathf.Sin(t * 3.2f));
            float roll = (stand ? 6f * Mathf.Sin(t * 2.2f) : 0f) + (mode == "turn" ? 14f * Mathf.Sin(ang * 2f) : 0f) + (mode == "fast" && !stand ? 6f : 0f);
            it.tr.position = pos + Vector3.up * bob; it.tr.rotation = Quaternion.LookRotation(tan, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(it.fwdLocal)) * Quaternion.AngleAxis(roll, it.fwdLocal);
            R.direct = true; R.faceDir = tan; R.faceCam = 0f;
            float top; Vector3 seatL;
            if (!stand)
            {
                if (it.seatT < 0f) { float lo = cat == "Horse" ? 0.3f : 0.15f, hi = cat == "Horse" ? 0.6f : 0.55f, best = -1f; for (int k = 0; k <= 12; k++) { float tt = Mathf.Lerp(lo, hi, k / 12f); LP(it, tt, 0, out float tp); if (tp > best) { best = tp; it.seatT = tt; } } it.seatTop = best; }          // the saddle / the horse's back: the highest point behind the handlebar - a fixed 3D point she always sits on
                seatL = LP(it, it.seatT, 0, out top);
            }
            else seatL = LP(it, 0.5f, 0, out top);
            Vector3 seatW = it.tr.TransformPoint(seatL);
            if (stand) { R.stand = seatW; R.legMode = 0; Vector3 side = Vector3.Cross(Vector3.up, tan); float k = Mathf.Sin(t * 2f); Hands(seatW + side * 0.45f + Vector3.up * (0.52f + 0.05f * k), seatW - side * 0.45f + Vector3.up * (0.52f - 0.05f * k)); }
            else
            {
                R.stand = new Vector3(seatW.x, seatW.y + 0.04f - G.HipHeight, seatW.z);          // hips exactly on the seat
                R.legMode = cat == "Horse" ? 1 : 2; R.legPhase = legT;
                float topH; Vector3 hb = LP(it, cat == "Horse" ? 0.78f : 0.82f, 0.2f, out topH); Vector3 hb2 = LP(it, cat == "Horse" ? 0.78f : 0.82f, -0.2f, out topH);
                Hands(it.tr.TransformPoint(hb), it.tr.TransformPoint(hb2));
            }
        }

        // ---- hit the punching bag / speed bag
        void Hit(Item it)
        {
            // what the video shows: hit = punches on the bag, fast = quick combinations, move = footwork in guard (stepping back and in, bouncing), idle = resting in guard
            string mode = R.Mode; bool punching = mode == "hit" || mode == "fast"; bool foot = mode != "idle" && mode != "stop"; float rate = mode == "fast" ? 11f : 7f;
            Vector3 p = it.slot; Vector3 to = Flat(frame.position - p).normalized; if (to.sqrMagnitude < 0.01f) to = -Vector3.forward; var wb = WorldBounds(it); float half = Mathf.Max(wb.extents.x, wb.extents.z);
            float step = foot ? 0.10f * Mathf.Sin(t * 1.6f) : 0f; float bounce = foot ? 0.025f * Mathf.Abs(Mathf.Sin(t * 6f)) : 0f;          // a little back and forth, light on her feet
            R.direct = true; R.legMode = 0;          // placed exactly: no walking legs while she shuffles
            R.stand = p + to * (half + 0.42f + step); R.stand.y = bounce; R.faceDir = -to;
            float s = punching ? Mathf.Sin(t * rate) : 0f; float ext = Mathf.Clamp01(s); float ext2 = Mathf.Clamp01(-s);
            float hy = Mathf.Min(0.85f, wb.size.y * 0.7f); Vector3 front = p + to * half + Vector3.up * hy; Vector3 side = Vector3.Cross(Vector3.up, to) * 0.12f;
            Vector3 guard = R.stand + R.faceDir * 0.2f + Vector3.up * (0.85f + 0.02f * Mathf.Sin(t * 6f));          // fists up by the chin
            Hands(Vector3.Lerp(guard + side, front + side, ext), Vector3.Lerp(guard - side, front - side, ext2));
            float sw = (prevHit <= 0f && s > 0f) || (prevHit >= 0f && s < 0f) ? 1f : 0f; if (sw > 0f && punching) ReenactSfx.Shot("chop", front, 0.8f); prevHit = s;
            Vector3 pivot = p + Vector3.up * wb.size.y; float swing = punching ? 7f * Mathf.Sin(t * 3.5f) * Mathf.Clamp01(0.4f + Mathf.Abs(s)) : 1.5f * Mathf.Sin(t * 1.5f); Vector3 axis = Vector3.Cross(Vector3.up, to);
            it.tr.rotation = Quaternion.AngleAxis(swing, axis) * it.slotRot; it.tr.position = pivot + Quaternion.AngleAxis(swing, axis) * (p - pivot);
        }

        // ---- climb the wall / cliff
        void Climb(Item it)
        {
            var wb = WorldBounds(it); Vector3 to = Flat(frame.position - wb.center).normalized; Vector3 face = wb.ClosestPoint(wb.center + to * 10f); face.y = 0;
            float up = Mathf.PingPong(t * 0.1f, 0.5f); R.stand = face + to * 0.34f; R.stand.y = up; R.faceDir = -to; R.direct = false; R.legMode = 3; R.legPhase = t * 2.4f;
            float s = Mathf.Sin(t * 2.4f); Vector3 side = Vector3.Cross(Vector3.up, to) * 0.14f; float baseY = up + 0.82f;
            Vector3 a = wb.ClosestPoint(new Vector3(R.stand.x, baseY + 0.12f + 0.1f * s, R.stand.z) - to * 0.5f) ; Vector3 b = wb.ClosestPoint(new Vector3(R.stand.x, baseY + 0.12f - 0.1f * s, R.stand.z) - to * 0.5f);
            Hands(a + side + to * 0.03f, b - side + to * 0.03f);
        }

        // ---- run up, jump over, run on, come back (boxes, walls, rails, ramps, stairs, benches)
        void Hop(Item it)
        {
            var wb = WorldBounds(it); Vector3 right = Vector3.Cross(Vector3.up, Flat(frame.position - wb.center).normalized); Vector3 c = new Vector3(wb.center.x, 0, wb.center.z); float half = Mathf.Max(wb.extents.x, wb.extents.z) + 0.2f; float H = wb.size.y;
            float T = 4.2f; float tt = t % T; Vector3 pos; float y = 0f; bool air = false; float run = half + 0.5f;
            if (tt < 1.1f) pos = c - right * Mathf.Lerp(run + 0.5f, run - 0.1f, tt / 1.1f);
            else if (tt < 1.9f) { float u = (tt - 1.1f) / 0.8f; pos = c + right * Mathf.Lerp(-half, half, u); y = H + 0.12f + 0.22f * Mathf.Sin(u * Mathf.PI); air = true; y = Mathf.Max(y, Mathf.Sin(u * Mathf.PI) * (H + 0.3f)); }
            else if (tt < 2.6f) pos = c + right * Mathf.Lerp(half, run + 0.5f, (tt - 1.9f) / 0.7f);
            else pos = c + right * Mathf.Lerp(run + 0.5f, run + 0.5f, 0f);
            if (tt >= 2.6f) { pos = c + right * (run + 0.5f); R.faceCam = Mathf.Clamp01((tt - 2.6f) * 2f); } else R.faceCam = 0f;
            R.direct = tt < 2.6f; R.stand = new Vector3(pos.x, y, pos.z); R.faceDir = tt < 2.6f ? right : -right; R.legMode = air ? 4 : 0; R.legPhase = t * 8f;
            if (air) { Vector3 sd = Vector3.Cross(Vector3.up, right); Hands(R.stand + Vector3.up * 1.0f + sd * 0.2f + right * 0.1f, R.stand + Vector3.up * 1.0f - sd * 0.2f + right * 0.1f); }
            else if (tt >= 2.6f) { Hands(R.stand + Vector3.up * 1.05f + right * 0.1f, R.stand + Vector3.up * 1.05f - right * 0.1f); }
        }

        // ---- pick up and show small things
        void Hold(Item it)
        {
            float T = 7f; float tt = t % T; Vector3 to = Flat(frame.position - it.slot).normalized; if (to.sqrMagnitude < 0.01f) to = Vector3.back; Vector3 stand = it.slot + to * 0.45f; R.stand = stand; R.faceDir = -to; R.direct = false;
            Vector3 itemTop = it.slot + Vector3.up * Mathf.Max(0.05f, it.size.y * it.s * 0.6f); Vector3 chest = stand - (-R.faceDir) * 0.0f + R.faceDir * 0.18f + Vector3.up * 0.72f; Vector3 show = stand + R.faceDir * 0.0f + Vector3.up * 0.82f;
            Vector3 rest = stand + R.faceDir * 0.1f + Vector3.up * 0.55f; Vector3 hp;
            if (tt < 1.0f) hp = Vector3.Lerp(rest, itemTop, tt); else if (tt < 1.4f) hp = itemTop; else if (tt < 2.3f) hp = Vector3.Lerp(itemTop, chest, (tt - 1.4f) / 0.9f);
            else if (tt < 2.9f) hp = Vector3.Lerp(chest, ShowPt(), (tt - 2.3f) / 0.6f); else if (tt < 5.0f) hp = ShowPt() + Vector3.up * Mathf.Sin(tt * 3f) * 0.01f;
            else if (tt < 5.9f) hp = Vector3.Lerp(ShowPt(), itemTop, (tt - 5.0f) / 0.9f); else hp = Vector3.Lerp(itemTop, rest, Mathf.Clamp01((tt - 5.9f) / 0.9f));
            R.tR = hp; R.hasR = true; bool carrying = tt >= 1.4f && tt < 5.9f; R.faceCam = tt > 2.3f && tt < 5.0f ? Mathf.Clamp01((tt - 2.3f) / 0.7f) : tt >= 5.0f && tt < 5.9f ? 1f - (tt - 5.0f) / 0.9f : 0f;
            R.Carry(carrying ? it.tr.gameObject : null, carrying, 0f); if (!carrying) { it.tr.SetPositionAndRotation(it.slot, it.slotRot); }
        }
        Vector3 ShowPt() { var g = G.transform; return g.position + g.forward * 0.27f + Vector3.up * 0.82f; }

        // ---- fly the plane
        void Fly(Item it)
        {
            float fa = t * 0.5f; Vector3 p = frame.position + frame.right * (Mathf.Cos(fa) * 2.2f) + frame.forward * (Mathf.Sin(fa) * 0.8f); Vector3 tan = (frame.right * (-Mathf.Sin(fa) * 2.2f) + frame.forward * (Mathf.Cos(fa) * 0.8f)).normalized;          // flies across your view along a wide oval
            R.direct = true; R.stand = new Vector3(p.x, 0, p.z); R.faceDir = tan; R.legMode = 0; R.legPhase = t * 7f;
            Vector3 pp = R.stand + Vector3.up * (1.05f + 0.06f * Mathf.Sin(t * 2f)) + tan * 0.18f; it.tr.position = pp; it.tr.rotation = Quaternion.LookRotation(tan, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(it.fwdLocal)) * Quaternion.AngleAxis(12f * Mathf.Sin(t * 1.5f), it.fwdLocal);
            Vector3 side = Vector3.Cross(Vector3.up, tan) * 0.09f; Hands(pp - Vector3.up * 0.04f + side, pp - Vector3.up * 0.04f - side);
        }

        // ---- swim among the fish
        void Swim(Item it)
        {
            R.direct = false; R.stand = frame.position + frame.TransformDirection(new Vector3(0, 0, 0.9f)); R.stand.y = 0.3f + 0.04f * Mathf.Sin(t * 1.5f); R.faceDir = frame.forward; R.legMode = 5; R.legPhase = t * 5f;
            Vector3 f = R.faceDir; Vector3 side = Vector3.Cross(Vector3.up, f); float k = Mathf.Sin(t * 2.4f);
            Vector3 sh = R.stand + Vector3.up * 0.75f; Hands(sh + f * (0.3f + 0.1f * k) + side * (0.1f + 0.2f * Mathf.Max(0, -k)), sh + f * (0.3f + 0.1f * k) - side * (0.1f + 0.2f * Mathf.Max(0, -k)));
        }
    }
}
