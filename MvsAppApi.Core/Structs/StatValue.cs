using Newtonsoft.Json;

namespace MvsAppApi.Core.Structs
{
    public class StatValue
    {
        [JsonProperty(PropertyName = "value")]
        public string Value { get; set; }
        [JsonProperty(PropertyName = "pct_detail")]
        public string PctDetail { get; set; }
    }
}