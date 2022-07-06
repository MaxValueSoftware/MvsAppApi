using System.Collections.Generic;
using Newtonsoft.Json;

namespace MvsAppApi.Core.Structs
{
    public class StatInfo
    {
        [JsonProperty(PropertyName = "stat")]
        public string Stat { get; set; }
        [JsonProperty(PropertyName = "desc")]
        public string Desc { get; set; }
        [JsonProperty(PropertyName = "value")]
        public string Value { get; set; }
        [JsonProperty(PropertyName = "format")]
        public string Format { get; set; }
        [JsonProperty(PropertyName = "type")]
        public string Type { get; set; }
        [JsonProperty(PropertyName = "categories")]
        public List<string> Categories { get; set; }
        [JsonProperty(PropertyName = "player_pct")]
        public bool PlayerPct { get; set; }
        [JsonProperty(PropertyName = "hud_safe")]
        public bool HudSafe { get; set; }
        [JsonProperty(PropertyName = "group_by")]
        public bool GroupBy { get; set; }
        [JsonProperty(PropertyName = "app_id")]
        public int AppId { get; set; }
        [JsonProperty(PropertyName = "flags")]
        public List<string> Flags { get; set; }
    }
}