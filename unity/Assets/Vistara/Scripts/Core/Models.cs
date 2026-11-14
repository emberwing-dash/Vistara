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
        public int feedback_votes;
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

    // ---- model info + held-out evaluation (engine: server/eval_api.py) ----
    [Serializable] public class HeadInfo { public string name; public string sha256_16; public long bytes; public float blend; public int best_epoch; public float heldout_zero_shot; public float heldout_trained; public int n_videos; }
    [Serializable] public class ModelInfo { public string engine_version; public string device; public string backbone; public string weights_dir; public HeadInfo[] heads; }
    [Serializable] public class EvalVideo { public string video_id; public string dataset; public float duration_sec; public string path; }
    [Serializable] public class EvalVideoList { public int count; public int manifest_total; public EvalVideo[] items; }
    [Serializable] public class EvalStatus { public string state; public int done; public int total; public string video; public string message; }
    [Serializable] public class EpicMetrics { public int videos; public int gt_actions; public float action_map; public float action_coverage; public float verb_acc; public float noun_acc; public float pair_acc; public float loc_recall_iou0_1; public float label_cooking_rate; }
    [Serializable] public class FpvMetrics { public int videos; public float label_accuracy; public int highlight_f1_videos; public float highlight_f1; }
    [Serializable] public class AllMetrics { public float schema_valid_rate; public float within_budget_rate; public float mean_highlight_frac; public float mean_infer_sec; public float median_realtime_factor; }
    [Serializable] public class EvalSummary { public string generated; public string device; public int videos_evaluated; public EpicMetrics epic; public FpvMetrics fpvsum; public AllMetrics all; public string note; }
    [Serializable] public class EvalRow { public string video_id; public string dataset; public float duration_sec; public string label_pred; public string label_true; public int n_actions; public float highlight_frac; public float infer_sec; public float verb_acc; public float noun_acc; public float pair_acc; public float action_map; public float coverage; public float highlight_f1; }
    [Serializable] public class EvalResults { public EvalSummary summary; public EvalRow[] per_video; }
}
