using System;

namespace Vistara
{
    // JSON contracts of server/journey_api.py (categories derived from manifest.csv video ids)
    [Serializable] public class CategoryInfo { public string id; public string name; public string dataset; public string label; public int manifest_count; public int available_count; }
    [Serializable] public class CategoryList { public CategoryInfo[] items; public string source; public int manifest_total; }
    [Serializable] public class CatVideo { public string video_id; public string dataset; public float duration_sec; public string path; }
    [Serializable] public class PlayableInfo { public string path; }
    [Serializable] public class CatVideoList { public string category; public int count; public CatVideo[] items; }
}
