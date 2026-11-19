using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vistara
{
    /// <summary>Video workspace: video surface + metadata + Analyze call-to-action.</summary>
    public class PlayerPanel : IPanel
    {
        readonly WorkspaceController ctx;
        RawImage surface; TextMeshProUGUI hint, meta, name_;

        public PlayerPanel(WorkspaceController c) { ctx = c; }

        public void Build(RectTransform root)
        {
            var frame = UIKit.Box(root, "Frame", Color.black, 22);
            UIKit.TL(frame.rectTransform, 0, 0, 940, 530);
            UIKit.Outline(frame.transform, Theme.Cyan * new Color(1, 1, 1, 0.5f), 22, 3f);
            var rt = UIKit.Rect(frame.transform, "Surface"); UIKit.Fill(rt, 6, 6, 6, 6);
            surface = rt.gameObject.AddComponent<RawImage>(); surface.texture = ctx.Source.Texture; surface.raycastTarget = false;
            hint = UIKit.TextAt(frame.transform, "No video selected.\nOpen the Library to import a first-person MP4.", 28, 0, 0, 940, 530, Theme.TextDim, TextAlignmentOptions.Center);

            var info = CardUI.Card(root, "Video info", 970, 0, 430, 530, Theme.Violet);
            name_ = UIKit.TextAt(info, "-", 28, 24, 62, 382, 80, Theme.Text, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            meta = UIKit.TextAt(info, "", 23, 24, 160, 382, 300, Theme.TextDim);
            meta.lineSpacing = 18;

            var cta = UIKit.Button(root, "ANALYZE THIS VIDEO", 0, 556, 940, 110, ctx.StartAnalysis, Theme.Cyan, Theme.Bg, Sprites.Icon.Bolt, 38, 55, true);
            UIKit.Button(root, "Choose another", 970, 556, 430, 110, () => ctx.SwitchTab(Workspace.Tab.Library), Theme.Card, Theme.Text, Sprites.Icon.Folder, 28, 55);
            UIKit.TextAt(root, "The engine detects actions, scores importance and compiles a ~15% highlight reel from the original footage.", 22, 0, 692, 1400, 60, Theme.TextDim);
        }

        public void SetVideo(string path)
        {
            name_.text = Path.GetFileName(path);
            meta.text = "Preparing video...";
            hint.gameObject.SetActive(false);
        }

        public void OnSourcePrepared()
        {
            var s = ctx.Source;
            long size = 0; try { size = new FileInfo(AppState.VideoPath).Length; } catch { }
            meta.text = $"Duration\n<color=#EAF1FF>{AppState.FormatTime(s.Length)}</color>\n\nResolution\n<color=#EAF1FF>{s.Width} x {s.Height}</color>\n\nFrame rate\n<color=#EAF1FF>{s.Player.frameRate:0.#} fps</color>\n\nSize\n<color=#EAF1FF>{size / (1024f * 1024f):0} MB</color>";
            s.Seek(0f);
        }

        public void Tick()
        {
            if (surface == null) return;
            if (ctx.Source.Error != null) { hint.gameObject.SetActive(true); hint.text = "Could not play this video in Unity\n(" + ctx.Source.Error + ")\nAnalysis still works."; }
        }
    }
}
