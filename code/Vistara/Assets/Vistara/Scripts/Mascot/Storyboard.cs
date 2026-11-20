using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Vistara
{
    /// <summary>
    /// The visual analysis: a storyboard of the video's key moments - the actual frames of the footage, each captioned with what the engine recognised there (verb + object) and when.
    /// The moment that is playing right now is lit; tapping a card jumps the video there. No charts: only real frames and plain-language captions.
    /// </summary>
    public class Storyboard : MonoBehaviour
    {
        const float W = 1040f, H = 262f, Sc = 0.00026f, CardW = 160f, CardH = 90f, Gap = 10f;
        class Card { public float t, start, end; public string verb, noun; public RawImage img; public Image outline; public RectTransform rt; }
        JourneyController journey; Canvas canvas; RectTransform rt; string builtFor; readonly List<Card> cards = new List<Card>(); TextMeshProUGUI nowLbl;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded += (s, m) => { if (s.name == "Journey") Spawn(); };
            if (SceneManager.GetActiveScene().name == "Journey") Spawn();
        }
        static void Spawn() { if (FindFirstObjectByType<Storyboard>() == null) new GameObject("Storyboard").AddComponent<Storyboard>(); }

        void Start()
        {
            journey = FindFirstObjectByType<JourneyController>();
            canvas = UIKit.WorldCanvas("Storyboard", W, H, null, Sc); rt = (RectTransform)canvas.transform; canvas.gameObject.SetActive(false);
        }

        static ActionItem BestAt(ActionItem[] acts, float a, float b)
        {
            ActionItem best = null; float bs = -1f; if (acts == null) return null;
            foreach (var x in acts) { float ov = Mathf.Min(b, x.End) - Mathf.Max(a, x.Start); if (ov > 0 && x.score + ov * 0.02f > bs) { bs = x.score + ov * 0.02f; best = x; } }
            if (best != null) return best;
            float mid = (a + b) * 0.5f; float bd = 1e9f; foreach (var x in acts) { float d = Mathf.Abs((x.Start + x.End) * 0.5f - mid); if (d < bd) { bd = d; best = x; } }
            return best;
        }

        void Rebuild(ResultEnvelope r, string path)
        {
            UIKit.ClearChildren(rt); cards.Clear(); var o = r.output; var acts = o.actions ?? new ActionItem[0];
            var back = UIKit.Box(rt, "Back", new Color(0.03f, 0.05f, 0.12f, 0.92f), 26, false); UIKit.Fill(back.rectTransform);
            UIKit.Outline(back.transform, Theme.Violet * new Color(1, 1, 1, 0.6f), 26, 3f);
            UIKit.TextAt(rt, "What happened", 26, 26, 14, 360, 40, Theme.Cyan, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

            // moments: the highlight segments (else the strongest actions), chronological, at most 6
            var moments = new List<Vector2>();
            foreach (var h in (o.highlights ?? new SegmentItem[0]).OrderBy(x => x.segment[0])) moments.Add(new Vector2(h.segment[0], h.segment[1]));
            if (moments.Count < 5)          // few highlights: add the strongest actions that are not already covered, so the storyboard tells the whole story
            {
                foreach (var x in acts.OrderByDescending(z => z.score))
                {
                    if (moments.Count >= 5) break; float mid0 = (x.Start + x.End) * 0.5f; bool covered = false;
                    foreach (var m in moments) if (mid0 >= m.x - 1.5f && mid0 <= m.y + 1.5f) { covered = true; break; }
                    if (!covered) moments.Add(new Vector2(x.Start, x.End));
                }
                moments = moments.OrderBy(m => m.x).ToList();
            }
            if (moments.Count > 6) { var keep = new List<Vector2>(); for (int i = 0; i < 6; i++) keep.Add(moments[Mathf.RoundToInt(i * (moments.Count - 1) / 5f)]); moments = keep; }

            // one plain sentence: the sequence of distinct actions in order
            var seq = new List<string>(); foreach (var x in acts.OrderBy(z => z.Start)) { string s = x.verb + " " + x.noun; if (seq.Count == 0 || seq[seq.Count - 1] != s) seq.Add(s); if (seq.Count >= 5) break; }
            UIKit.TextAt(rt, seq.Count > 0 ? string.Join("  →  ", seq) : "", 21, 26, 50, W - 52, 30, new Color(0.82f, 0.93f, 1f), TextAlignmentOptions.MidlineLeft);

            float x0 = (W - (moments.Count * CardW + (moments.Count - 1) * Gap)) * 0.5f;
            for (int i = 0; i < moments.Count; i++)
            {
                float a = moments[i].x, b = moments[i].y; var act = BestAt(acts, a, b); float mid = (a + b) * 0.5f;
                var c = new Card { t = mid, start = a, end = b, verb = act != null ? act.verb : "", noun = act != null ? act.noun : "" };
                float x = x0 + i * (CardW + Gap); const float y = 92f;
                var holder = UIKit.Rect(rt, "Card"); UIKit.TL(holder, x, y, CardW, 150f); c.rt = holder;
                var frame = UIKit.Box(holder, "Frame", new Color(0.08f, 0.1f, 0.2f, 1f), 12); UIKit.TL(frame.rectTransform, 0, 0, CardW, CardH);
                var rawGo = UIKit.Rect(holder, "Img"); UIKit.TL(rawGo, 3, 3, CardW - 6, CardH - 6); c.img = rawGo.gameObject.AddComponent<RawImage>(); c.img.color = new Color(1, 1, 1, 0.15f); c.img.raycastTarget = false;
                c.outline = UIKit.Outline(holder, Theme.Orange, 12, 4f); UIKit.TL((RectTransform)c.outline.transform, 0, 0, CardW, CardH); c.outline.color = new Color(1, 1, 1, 0f);
                string tag = c.verb.Length > 0 ? c.verb : "moment";
                UIKit.TextAt(holder, tag, 22, 2, CardH + 4, CardW - 4, 28, Theme.Text, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
                UIKit.TextAt(holder, c.noun, 20, 2, CardH + 30, CardW - 4, 26, Theme.Orange, TextAlignmentOptions.MidlineLeft);
                UIKit.TextAt(holder, AppState.FormatTime(a), 16, 2, CardH + 54, CardW - 4, 22, Theme.TextDim, TextAlignmentOptions.MidlineLeft);
                var btn = frame.gameObject.AddComponent<Button>(); btn.transition = Selectable.Transition.None; btn.targetGraphic = frame; float seekT = a;
                btn.onClick.AddListener(() => { if (journey != null && journey.Source != null) journey.Source.Seek(seekT); });
                cards.Add(c);
                if (!string.IsNullOrEmpty(path) && BackendClient.Instance != null) StartCoroutine(LoadFrame(c, path, mid));
            }
            nowLbl = UIKit.TextAt(rt, "", 20, 26, H - 34, W - 52, 28, Theme.Text, TextAlignmentOptions.MidlineLeft);
            builtFor = o.video_id + "|" + acts.Length + "|";
        }

        IEnumerator LoadFrame(Card c, string path, float t)
        {
            Texture2D tex = null;
            yield return BackendClient.Instance.LoadTexture("/frame?path=" + UnityEngine.Networking.UnityWebRequest.EscapeURL(path) + "&t=" + t.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "&w=360", x => tex = x);
            if (tex != null && c.img != null) { c.img.texture = tex; c.img.color = Color.white; }
        }

        void Update()
        {
            if (journey == null) { journey = FindFirstObjectByType<JourneyController>(); return; }
            bool on = journey.Hud != null && journey.Hud.Visible && AppState.HasResult && journey.Hud.Root != null && canvas != null;
            if (canvas == null) return;
            if (canvas.gameObject.activeSelf != on) canvas.gameObject.SetActive(on);
            if (!on) return;
            var r = AppState.Result; var o = r.output; string path = journey.SelectedPath ?? AppState.VideoPath;
            if (builtFor != o.video_id + "|" + (o.actions != null ? o.actions.Length : 0) + "|") Rebuild(r, path);
            var frame = journey.Hud.Root; float s = frame.lossyScale.x;
            canvas.transform.SetPositionAndRotation(frame.TransformPoint(new Vector3(0f, (640f * Sc * 0.5f) + (H * Sc * 0.5f) + 0.02f, 0f)), frame.rotation);
            canvas.transform.localScale = Vector3.one * (Sc * s);
            var vc = journey.Source; if (vc == null || !vc.Prepared) return;
            float t = vc.TimeSec; int cur = -1; float bd = 1e9f;
            for (int i = 0; i < cards.Count; i++) { var c = cards[i]; float d = (t >= c.start && t <= c.end) ? 0f : Mathf.Min(Mathf.Abs(t - c.start), Mathf.Abs(t - c.end)); if (d < bd) { bd = d; cur = i; } }
            for (int i = 0; i < cards.Count; i++)
            {
                bool lit = i == cur && bd < 1.5f; var c = cards[i];
                c.outline.color = Color.Lerp(c.outline.color, lit ? Color.white : new Color(1, 1, 1, 0f), 12f * Time.deltaTime);
                c.rt.localScale = Vector3.Lerp(c.rt.localScale, Vector3.one * (lit ? 1.07f : 1f), 10f * Time.deltaTime);
            }
            if (nowLbl != null)
            {
                string now = ""; if (o.actions != null) { ActionItem best = null; foreach (var a in o.actions) if (a.Start <= t && t <= a.End && (best == null || a.score >= best.score)) best = a; if (best != null) now = "Now: <b>" + (best.verb + " " + best.noun).Replace("<", "") + "</b>"; }
                nowLbl.text = now;
            }
        }
    }
}
