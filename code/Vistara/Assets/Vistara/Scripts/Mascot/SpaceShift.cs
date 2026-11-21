using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Vistara
{
    /// <summary>
    /// The "spatial shift" between worlds (same recipe as Cosmic Warrior's reality shift): chromatic aberration + lens squeeze + bloom + vignette ramp up,
    /// star streaks rush past you, the world is swapped at the peak, then everything eases out. Slightly slower than the game version so it is comfortable in a headset.
    /// </summary>
    public class SpaceShift : MonoBehaviour
    {
        public static SpaceShift Instance { get; private set; }
        public bool Busy { get; private set; }

        Volume volume; Bloom bloom; ChromaticAberration chroma; LensDistortion lens; Vignette vignette;
        ParticleSystem streaks; float amount;
        float baseBloom = 0.2f; float lensMax = -0.32f, scaleDrop = 0.08f, bloomGain = 2.6f, vignetteMax = 0.45f, chromaGain = 1.2f, fovGain = 0f, baseFov = 60f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if (Instance == null) { var go = new GameObject("SpaceShift"); go.AddComponent<SpaceShift>(); DontDestroyOnLoad(go); } }

        void Awake()
        {
            Instance = this;
            volume = gameObject.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 90; volume.weight = 1f;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            bloom = p.Add<Bloom>(true); bloom.threshold.Override(0.9f); bloom.intensity.Override(0f); bloom.scatter.Override(0.7f);
            chroma = p.Add<ChromaticAberration>(true); chroma.intensity.Override(0f);
            lens = p.Add<LensDistortion>(true); lens.intensity.Override(0f);
            vignette = p.Add<Vignette>(true); vignette.intensity.Override(0f); vignette.smoothness.Override(0.6f); vignette.color.Override(new Color(0.02f, 0.03f, 0.1f));
            volume.profile = p;
        }
        void OnDestroy() { if (Instance == this) Instance = null; }

        // ---------------------------------------------------------------- sound: a synthesised warp whoosh (rising noise + sweep, a boom at the swap, a shimmer on the way out)
        AudioSource sfx; AudioClip whoosh;
        static AudioClip MakeWhoosh()
        {
            const int sr = 44100; const float T = 2.0f, peak = 0.55f; int n = (int)(sr * T); var d = new float[n]; var rng = new System.Random(5);
            float lp = 0f, ph1 = 0f, ph2 = 0f, ph3 = 0f, ph4 = 0f, boomPh = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float k = t < peak ? t / peak : 1f - Mathf.Clamp01((t - peak) / (T - peak - 0.1f));
                float env = t < peak ? Mathf.Pow(k, 1.6f) : Mathf.Pow(k, 1.3f);
                float cutoff = t < peak ? Mathf.Lerp(300f, 6500f, k) : Mathf.Lerp(380f, 6500f, k);
                float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / sr);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0); lp += a * (noise - lp);
                float f1 = t < peak ? Mathf.Lerp(180f, 1400f, k * k) : Mathf.Lerp(300f, 1400f, k); ph1 += 2f * Mathf.PI * f1 / sr;
                float sweep = Mathf.Sin(ph1) * 0.22f + Mathf.Sin(ph1 * 1.5f) * 0.08f;
                float boomT = t - peak; float boom = 0f;
                if (boomT >= 0f) { float bf = Mathf.Lerp(90f, 38f, Mathf.Clamp01(boomT / 0.5f)); boomPh += 2f * Mathf.PI * bf / sr; boom = Mathf.Sin(boomPh) * Mathf.Exp(-boomT * 5.5f) * 0.95f; }
                float shim = 0f; if (boomT > 0f) { ph3 += 2f * Mathf.PI * 2200f / sr; ph4 += 2f * Mathf.PI * 3300f / sr; shim = (Mathf.Sin(ph3) + 0.6f * Mathf.Sin(ph4)) * Mathf.Exp(-boomT * 2.6f) * (0.5f + 0.5f * Mathf.Sin(boomT * 38f)) * 0.07f; }
                float fadeIn = Mathf.Clamp01(t / 0.03f), fadeOut = Mathf.Clamp01((T - t) / 0.15f);
                d[i] = (lp * 1.9f * env * 0.55f + sweep * env + boom + shim) * fadeIn * fadeOut;
            }
            float mx = 0.001f; for (int i = 0; i < n; i++) mx = Mathf.Max(mx, Mathf.Abs(d[i])); for (int i = 0; i < n; i++) d[i] = d[i] / mx * 0.8f;
            var c = AudioClip.Create("SpaceShiftWhoosh", n, 1, sr, false); c.SetData(d, 0); return c;
        }
        void PlaySfx()
        {
            if (whoosh == null) whoosh = MakeWhoosh();
            if (sfx == null) { sfx = gameObject.AddComponent<AudioSource>(); sfx.spatialBlend = 0f; sfx.playOnAwake = false; sfx.volume = 0.75f; }
            sfx.PlayOneShot(whoosh);
        }

        ParticleSystem MakeStreaks(Transform cam)
        {
            var go = new GameObject("ShiftStreaks"); go.transform.SetParent(cam, false); go.transform.localPosition = new Vector3(0, 0, 30f); go.transform.localRotation = Quaternion.Euler(0, 180f, 0);
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = true; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 0.7f; main.startSpeed = 70f; main.startSize = 0.06f; main.maxParticles = 700;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.6f, 0.95f, 1f, 0.95f), new Color(1f, 0.6f, 0.95f, 0.95f));
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 7f; sh.radiusThickness = 0.35f; sh.arc = 360f;
            var em = ps.emission; em.rateOverTime = 0f;
            var rd = go.GetComponent<ParticleSystemRenderer>(); rd.renderMode = ParticleSystemRenderMode.Stretch; rd.lengthScale = 14f; rd.velocityScale = 0.35f; rd.cameraVelocityScale = 0f;
            rd.sharedMaterial = new Material(Shader.Find("Sprites/Default")); rd.shadowCastingMode = ShadowCastingMode.Off; rd.receiveShadows = false;
            return ps;
        }

        public float Amount { get { return amount; } }

        // ---------------------------------------------------------------- the whole 3D space shifts
        // Everything that makes up the environment (the category world's props, or the neon room of the menu scenes) is flung outward from you, tilted and swollen as the shift peaks,
        // and the NEW environment starts out displaced the same way and settles into place while the effect eases out. Objects with colliders never move, so you cannot fall.
        class Base { public Vector3 p, s; public Quaternion r; public float h1, h2; }
        readonly Dictionary<Transform, Base> warped = new Dictionary<Transform, Base>(); readonly HashSet<Transform> rejected = new HashSet<Transform>();
        static readonly string[] KeepNames = { "XR", "Origin", "Rig", "Hand", "Controller", "Simulator", "Stara", "VeeGuide", "SpaceShift", "Camera", "EventSystem", "Canvas", "Stage", "Journey", "Sky", "SafeFloor", "Light", "Audio", "Mascot", "Vee" };

        bool Eligible(Transform t)
        {
            if (t == null || rejected.Contains(t)) return false;
            string n = t.name; foreach (var k in KeepNames) if (n.Contains(k)) { rejected.Add(t); return false; }
            if (t.GetComponentInChildren<Collider>(true) != null || t.GetComponentInChildren<Camera>(true) != null || t.GetComponentInChildren<Canvas>(true) != null || t.GetComponentInChildren<Light>(true) != null || t.GetComponentInChildren<AudioListener>(true) != null) { rejected.Add(t); return false; }
            if (t.GetComponentInChildren<Renderer>(true) == null) { rejected.Add(t); return false; }
            return true;
        }
        void Gather()
        {
            var env = CategoryEnvironment.Instance; if (env != null) foreach (var t in env.WarpTargets()) if (!warped.ContainsKey(t) && Eligible(t)) Add(t);
            var sc = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (sc.IsValid()) foreach (var go in sc.GetRootGameObjects())
                {
                    var t = go.transform; if (warped.ContainsKey(t)) continue;
                    if (Eligible(t)) { Add(t); continue; }
                    if (t.name == "Environment" || t.name == "Dust") for (int i = 0; i < t.childCount; i++) { var c = t.GetChild(i); if (!warped.ContainsKey(c) && c.GetComponentInChildren<Collider>(true) == null && c.GetComponentInChildren<Renderer>(true) != null) Add(c); }       // the neon room: its parts fly outward too (the floor collider stays so you cannot fall)
                }
        }
        void Add(Transform t)
        {
            int h = t.GetInstanceID() * 73856093; float h1 = Mathf.Abs((h % 1000) / 1000f), h2 = Mathf.Abs(((h >> 8) % 1000) / 1000f);
            warped[t] = new Base { p = t.position, r = t.rotation, s = t.localScale, h1 = h1, h2 = h2 };
        }
        void LateUpdate()
        {
            if (amount <= 0.0005f && warped.Count == 0) return;
            // no Gather(): the 3D environment is not displaced, only the post-processing pulse runs
            var cam = Camera.main; Vector3 c = cam != null ? cam.transform.position : Vector3.zero; float e = Mathf.SmoothStep(0f, 1f, amount); List<Transform> dead = null;
            foreach (var kv in warped)
            {
                var t = kv.Key; if (t == null) { (dead ?? (dead = new List<Transform>())).Add(t); continue; }
                var b = kv.Value;
                if (amount <= 0.0005f) { t.position = b.p; t.rotation = b.r; t.localScale = b.s; continue; }
                Vector3 d = b.p - c; d.y = 0f; float dist = d.magnitude; Vector3 dir = dist > 0.01f ? d / dist : Quaternion.Euler(0, b.h1 * 360f, 0) * Vector3.forward;
                float push = (6f + dist * 1.6f) * e * (0.6f + 0.8f * b.h2);
                t.position = b.p + dir * push + Vector3.up * (e * (b.h1 - 0.5f) * 5f);
                t.rotation = Quaternion.Euler((b.h1 - 0.5f) * 60f * e, (b.h2 - 0.5f) * 110f * e, (b.h1 - 0.5f) * 40f * e) * b.r;
                t.localScale = b.s * (1f + 0.45f * e);
            }
            if (dead != null) foreach (var t in dead) warped.Remove(t);
            if (amount <= 0.0005f) warped.Clear();
        }

        void SetAmount(float k)
        {
            amount = Mathf.Clamp01(k);
            chroma.intensity.value = Mathf.Clamp01(amount * chromaGain);
            lens.intensity.value = lensMax * amount; lens.scale.value = 1f - scaleDrop * amount;
            bloom.intensity.value = baseBloom + amount * bloomGain;
            vignette.intensity.value = vignetteMax * amount;
            if (fovGain > 0f && Camera.main != null && !Camera.main.stereoEnabled) Camera.main.fieldOfView = baseFov + fovGain * amount;
            if (streaks != null) { var em = streaks.emission; em.rateOverTime = 520f * amount; }
        }

        /// <summary>Plays the shift; <paramref name="swap"/> runs at the peak (when the view is fully "in" the effect).</summary>
        /// <summary>The main-menu "Start": exactly the same spatial shift as between categories (Cosmic Warrior's glitch shift), with the next scene loaded at the peak.</summary>
        public void Dive(string scene) { if (!Busy) StartCoroutine(Run(() => UnityEngine.SceneManagement.SceneManager.LoadScene(scene))); }

        public IEnumerator Run(Action swap, bool outward = false, float up = 0.32f, float hold = 0.18f, float down = 0.6f)
        {
            outward = false;          // exactly Cosmic Warrior's spatial shift: a post-processing pulse (chromatic aberration, lens, bloom, vignette) with the swap at its peak; the 3D environment is NOT displaced
            if (outward) { lensMax = -0.6f; scaleDrop = 0.16f; bloomGain = 3.2f; vignetteMax = 0.55f; chromaGain = 1.5f; fovGain = 28f; baseFov = Camera.main != null ? Camera.main.fieldOfView : 60f; }
            else { lensMax = -0.55f; scaleDrop = 0.12f; bloomGain = 3.0f; vignetteMax = 0.3f; chromaGain = 1.2f; fovGain = 0f; }       // Cosmic Warrior's PostFX values
            if (Camera.main != null) { var data = Camera.main.GetUniversalAdditionalCameraData(); if (data != null) data.renderPostProcessing = true; }
            Busy = true; PlaySfx(); float t = 0f; Debug.Log("[Shift] start " + Time.unscaledTime.ToString("0.00") + " targets=" + warped.Count);
            while (t < up) { t += Time.unscaledDeltaTime; SetAmount(Mathf.SmoothStep(0, 1, t / up)); yield return null; }
            SetAmount(1f);
            Debug.Log("[Shift] peak " + Time.unscaledTime.ToString("0.00") + " targets=" + warped.Count + " lens=" + lens.intensity.value.ToString("0.00")); swap?.Invoke();
            if (outward) { var nc = Camera.main; if (nc != null) { var d2 = nc.GetUniversalAdditionalCameraData(); if (d2 != null) d2.renderPostProcessing = true; baseFov = nc.fieldOfView; } }
            yield return new WaitForSecondsRealtime(hold);
            t = 0f;
            while (t < down) { t += Time.unscaledDeltaTime; SetAmount(1f - Mathf.SmoothStep(0, 1, t / down)); yield return null; }
            SetAmount(0f); if (outward && Camera.main != null && !Camera.main.stereoEnabled) Camera.main.fieldOfView = baseFov; 
            Busy = false;
        }
    }
}
