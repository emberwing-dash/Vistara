using TMPro;
using UnityEngine;

namespace Vistara
{
    /// <summary>
    /// A 3D overlay on the object Vee is using: pulsing neon-green rings around it (always facing you), four corner brackets and a floating label with the object and the verb.
    /// It lives in the world (not on a canvas), so it stays visible when the UI is hidden with Y.
    /// </summary>
    public class ObjectOverlay : MonoBehaviour
    {
        GuideGirl girl; Reenactor reen; LineRenderer ring, ring2; LineRenderer[] brackets = new LineRenderer[4]; TextMeshPro label; Transform root;
        Vector3 anchor; bool shown;
        static readonly Color Green = new Color(0.24f, 1f, 0.48f, 1f);

        void Start()
        {
            girl = GetComponent<GuideGirl>(); reen = GetComponent<Reenactor>();
            root = new GameObject("ObjectOverlay").transform; root.SetParent(null, false);
            ring = MakeLine("Ring", 48, 0.012f, true); ring2 = MakeLine("Ring2", 48, 0.006f, true);
            for (int i = 0; i < 4; i++) brackets[i] = MakeLine("Corner" + i, 3, 0.01f, false);
            var lg = new GameObject("Label"); lg.transform.SetParent(root, false);
            label = lg.AddComponent<TextMeshPro>(); label.alignment = TextAlignmentOptions.Center; label.fontSize = 1.4f; label.fontStyle = FontStyles.Bold; label.color = Color.white;
            label.rectTransform.sizeDelta = new Vector2(3f, 0.6f); label.enableWordWrapping = false; label.outlineWidth = 0.25f; label.outlineColor = new Color32(10, 40, 20, 255);
            root.gameObject.SetActive(false);
        }

        LineRenderer MakeLine(string n, int pts, float width, bool loop)
        {
            var g = new GameObject(n); g.transform.SetParent(root, false); var l = g.AddComponent<LineRenderer>();
            l.useWorldSpace = false; l.loop = loop; l.positionCount = pts; l.widthMultiplier = width; l.numCapVertices = 4;
            var sh = Shader.Find("Sprites/Default"); l.sharedMaterial = new Material(sh != null ? sh : Shader.Find("Universal Render Pipeline/Unlit"));
            l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; l.receiveShadows = false; l.startColor = l.endColor = Green; return l;
        }

        void OnDestroy() { if (root != null) Destroy(root.gameObject); }

        void LateUpdate()
        {
            if (root == null || reen == null || girl == null) return;
            var cam = Camera.main; bool on = reen.Active && !string.IsNullOrEmpty(reen.CurNoun) && cam != null;
            if (!on) { if (shown) { root.gameObject.SetActive(false); shown = false; } return; }
            Vector3 p;
            if (reen.hasR && reen.hasL) p = (reen.tR + reen.tL) * 0.5f; else if (reen.hasR) p = reen.tR; else if (reen.hasL) p = reen.tL; else p = girl.transform.position + Vector3.up * 0.9f;
            anchor = shown ? Vector3.Lerp(anchor, p, 1f - Mathf.Exp(-10f * Time.deltaTime)) : p;
            if (!shown) { root.gameObject.SetActive(true); shown = true; }
            root.position = anchor; Vector3 toCam = cam.transform.position - anchor; root.rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);          // local +Z away from the user: the rings face the user
            float k = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f); float r = 0.16f + 0.015f * k;
            Circle(ring, r, 0f); Circle(ring2, r * 0.72f, 0.4f);
            var c1 = new Color(Green.r, Green.g, Green.b, 0.55f + 0.4f * k); ring.startColor = ring.endColor = c1; ring2.startColor = ring2.endColor = new Color(0.1f, 0.9f, 1f, 0.7f - 0.3f * k);
            float s = r * 1.25f, a = r * 0.45f; Vector2[] cs = { new Vector2(-1, 1), new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, -1) };
            for (int i = 0; i < 4; i++)
            {
                var c = cs[i] * s; brackets[i].SetPosition(0, new Vector3(c.x - cs[i].x * a, c.y, 0)); brackets[i].SetPosition(1, new Vector3(c.x, c.y, 0)); brackets[i].SetPosition(2, new Vector3(c.x, c.y - cs[i].y * a, 0));
                brackets[i].startColor = brackets[i].endColor = c1;
            }
            string n = reen.CurNoun.Replace(':', ' ').Replace('_', ' '); label.text = n.ToUpperInvariant() + "\n<size=60%><color=#9dffbf>" + reen.CurVerb + "</color></size>";
            label.transform.localPosition = new Vector3(0, s + 0.1f, 0); label.transform.localRotation = Quaternion.identity; label.transform.localScale = Vector3.one * 0.12f;
        }

        static void Circle(LineRenderer l, float r, float phase)
        {
            int n = l.positionCount; for (int i = 0; i < n; i++) { float a = (i / (float)n) * Mathf.PI * 2f + phase; l.SetPosition(i, new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0)); }
        }
    }
}
