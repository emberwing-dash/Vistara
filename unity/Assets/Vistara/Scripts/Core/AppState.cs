using System;
using UnityEngine;

namespace Vistara
{
    /// <summary>Process-wide state that survives scene loads (selected video, current job, last result).</summary>
    public static class AppState
    {
        public static string VideoPath;
        public static string Category;            // Journey: selected category id (Kitchen, Bike, ...)
        public static string JobId;
        public static ResultEnvelope Result;
        public static JobStatus LastJob;
        public static Workspace.Tab PendingTab = Workspace.Tab.Library;

        public static bool HasVideo => !string.IsNullOrEmpty(VideoPath) && System.IO.File.Exists(VideoPath);
        public static bool HasResult => Result != null && Result.output != null;

        public static void Reset() { VideoPath = null; JobId = null; Result = null; LastJob = null; }

        public static string FormatTime(float s)
        {
            if (s < 0 || float.IsNaN(s)) s = 0;
            int t = Mathf.FloorToInt(s);
            return t >= 3600 ? $"{t / 3600}:{(t % 3600) / 60:00}:{t % 60:00}" : $"{t / 60:00}:{t % 60:00}";
        }
    }

    public static class Workspace
    {
        public enum Tab { Library, Player, Analysis, Timeline, Summary, TestLab }
    }
}
