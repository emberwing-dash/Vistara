using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vistara
{
    /// <summary>What the file browser needs from its host screen (Workspace tabs or the Journey flow).</summary>
    public interface IVideoHost
    {
        void SelectVideo(string path);
        void Toast(string msg, float secs = 3f);
        void StartUrlAnalysis(string url);
    }

    /// <summary>In-VR file browser (grid of folders and videos, paged) used to import first-person MP4s.</summary>
    public class FileBrowserPanel : IPanel
    {
        static readonly string[] Exts = { ".mp4", ".mov", ".m4v", ".avi", ".mkv", ".webm" };
        const int Cols = 5, Rows = 3, PerPage = Cols * Rows;

        readonly IVideoHost ctx;
        public bool showUrlButton = true;
        RectTransform grid; TextMeshProUGUI pathLabel, pageLabel, emptyLabel;
        List<(string path, bool isDir)> entries = new List<(string, bool)>();
        string dir; int page;
        bool built;

        public FileBrowserPanel(IVideoHost c) { ctx = c; }

        public void Build(RectTransform root)
        {
            float W = WorkspaceController.CW;
            // quick locations
            var places = new List<(string name, string path)>();
            void Add(string n, string p) { if (!string.IsNullOrEmpty(p) && Directory.Exists(p)) places.Add((n, p)); }
            Add("Videos", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
            Add("Desktop", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            Add("Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            Add("Downloads", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));
            Add("Samples", Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Backend", "data", "samples")));
            float x = 0;
            foreach (var p in places.Take(4))
            {
                var path = p.path;
                UIKit.Button(root, p.name, x, 0, 200, 58, () => Navigate(path), Theme.Card, Theme.Text, Sprites.Icon.Folder, 22, 18);
                x += 212;
            }
            foreach (var d in DriveInfo.GetDrives().Where(d => { try { return d.IsReady; } catch { return false; } }).Take(2))
            {
                var path = d.RootDirectory.FullName;
                UIKit.Button(root, d.Name.TrimEnd('\\', '/'), x, 0, 110, 58, () => Navigate(path), Theme.Card, Theme.TextDim, null, 22, 18);
                x += 120;
            }

            if (showUrlButton) UIKit.Button(root, "Paste YouTube link", W - 300, 0, 300, 58, () => ctx.StartUrlAnalysis(GUIUtility.systemCopyBuffer), Theme.Magenta, Theme.Text, Sprites.Icon.Download, 20, 18, true);
            UIKit.Button(root, "", 0, 74, 70, 58, Up, Theme.Card, Theme.Text, Sprites.Icon.Up, 20, 16);
            var pathBg = UIKit.Box(root, "Path", new Color(0, 0, 0, 0.3f), 14, false); UIKit.TL(pathBg.rectTransform, 82, 74, W - 82, 58);
            pathLabel = UIKit.TextAt(pathBg.transform, "", 22, 18, 0, W - 120, 58, Theme.TextDim, TextAlignmentOptions.MidlineLeft);
            pathLabel.overflowMode = TextOverflowModes.Ellipsis;

            grid = UIKit.Rect(root, "Grid"); UIKit.TL(grid, 0, 150, W, 530);

            UIKit.Button(root, "", W / 2 - 230, 704, 80, 66, () => SetPage(page - 1), Theme.Card, Theme.Text, Sprites.Icon.ChevronL, 20, 18);
            pageLabel = UIKit.TextAt(root, "Page 1/1", 26, W / 2 - 140, 704, 280, 66, Theme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            UIKit.Button(root, "", W / 2 + 150, 704, 80, 66, () => SetPage(page + 1), Theme.Card, Theme.Text, Sprites.Icon.Chevron, 20, 18);
            UIKit.TextAt(root, "Pick a first-person MP4 (2-10 min). Folders open on click.", 20, 0, 724, 520, 30, Theme.TextDim);
            emptyLabel = UIKit.TextAt(root, "No videos or folders here.", 28, 0, 330, W, 50, Theme.TextDim, TextAlignmentOptions.Center);
            emptyLabel.gameObject.SetActive(false);
            built = true;

            // open the project's Samples folder first when it holds videos (neutral, demo-friendly); otherwise the user's Videos
            string start = places.Count > 0 ? places[0].path : Application.persistentDataPath;
            var samples = places.FirstOrDefault(p => p.name == "Samples").path;
            if (!string.IsNullOrEmpty(samples) && Directory.GetFiles(samples).Any(f => Exts.Contains(Path.GetExtension(f).ToLowerInvariant()))) start = samples;
            Navigate(start);
        }

        public void OnShow() { if (built && dir != null) Refresh(); }

        void Up()
        {
            if (dir == null) return;
            var parent = Directory.GetParent(dir);
            if (parent != null) Navigate(parent.FullName);
        }

        void Navigate(string path)
        {
            dir = path; page = 0; Refresh();
        }

        void Refresh()
        {
            pathLabel.text = dir;
            entries.Clear();
            try
            {
                foreach (var d in Directory.GetDirectories(dir).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    var name = Path.GetFileName(d);
                    if (name.StartsWith(".") || name.StartsWith("$")) continue;
                    entries.Add((d, true));
                }
                foreach (var f in Directory.GetFiles(dir).Where(f => Exts.Contains(Path.GetExtension(f).ToLowerInvariant())).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                    entries.Add((f, false));
            }
            catch (Exception e) { ctx.Toast("Cannot open folder: " + e.Message); }
            SetPage(0);
        }

        void SetPage(int p)
        {
            int pages = Mathf.Max(1, Mathf.CeilToInt(entries.Count / (float)PerPage));
            page = Mathf.Clamp(p, 0, pages - 1);
            pageLabel.text = $"Page {page + 1}/{pages}";
            emptyLabel.gameObject.SetActive(entries.Count == 0);
            UIKit.ClearChildren(grid);
            float cw = (WorkspaceController.CW - (Cols - 1) * 16) / Cols, ch = 166;
            for (int i = 0; i < PerPage; i++)
            {
                int idx = page * PerPage + i;
                if (idx >= entries.Count) break;
                var e = entries[idx];
                int col = i % Cols, row = i / Cols;
                var card = UIKit.Button(grid, "", col * (cw + 16), row * (ch + 16), cw, ch, null, e.isDir ? Theme.Card : Theme.PanelHi, Theme.Text, null, 20, 20);
                var path = e.path; bool isDir = e.isDir;
                card.onClick.AddListener(() => { if (isDir) Navigate(path); else ctx.SelectVideo(path); });
                var ic = UIKit.Icon(card.transform, isDir ? Sprites.Icon.Folder : Sprites.Icon.Film, isDir ? Theme.Orange : Theme.Cyan, cw / 2 - 36, 14, 72);
                string name = Path.GetFileName(e.path);
                UIKit.TextAt(card.transform, name, 19, 10, 92, cw - 20, 48, Theme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
                string sub = isDir ? "Folder" : SizeStr(path);
                UIKit.TextAt(card.transform, sub, 17, 10, 138, cw - 20, 24, Theme.TextDim, TextAlignmentOptions.Center);
            }
        }

        static string SizeStr(string p)
        {
            try { long b = new FileInfo(p).Length; return b > 1 << 30 ? $"{b / (float)(1 << 30):0.0} GB" : $"{b / (float)(1 << 20):0} MB"; }
            catch { return ""; }
        }
    }
}
