using System;

namespace Vistara
{
    // JSON contracts shared with the Python engine (see Backend/server/app.py). Official output schema = OutputJson.
    [Serializable] public class LabelInfo { public string name; public float score; }
    [Serializable] public class ActionItem { public float[] segment; public string verb; public string noun; public float score; public float Start => segment[0]; public float End => segment[1]; }
    [Serializable] public class SegmentItem { public float[] segment; }

    [Serializable]
    public class OutputJson
    {
        public string team_id; public string video_id; public float duration_sec;
        public LabelInfo label; public ActionItem[] actions; public SegmentItem[] highlights;
    }

    [Serializable] public class Curve { public float[] t; public float[] importance; public float[] activity; }
    [Serializable] public class ObjStat { public string noun; public int count; public float seconds; public float score; }
    [Serializable] public class VerbStat { public string verb; public float seconds; }
    [Serializable] public class SegAnalysis { public float start; public float end; public float importance; }
    [Serializable] public class Timings { public float decode_s; public float embed_s; public float recognise_s; public float select_s; public float total_s; }

    [Serializable]
    public class AnalysisJson
    {
        public string engine_version; public string domain; public string device; public string model;
        public float retained_fraction; public float budget_seconds;
        public Curve curve; public ObjStat[] objects; public VerbStat[] verbs; public SegAnalysis[] segments; public Timings timings;
    }

    [Serializable]
    public class ResultEnvelope
    {
        public OutputJson output; public AnalysisJson analysis; public string highlight_path; public string source_path;
    }

    [Serializable]
    public class JobStatus
    {
        public string id; public string video_id; public string filename; public string status; public string stage;
        public float progress; public string message; public string error; public string source;
    }

    [Serializable] public class JobHandle { public string job_id; }
    [Serializable] public class JobList { public JobStatus[] items; }
    [Serializable] public class HealthInfo { public string status; public bool cuda; public bool model_loaded; }
}
