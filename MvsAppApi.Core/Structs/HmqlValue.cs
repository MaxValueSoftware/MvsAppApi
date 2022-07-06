using Newtonsoft.Json;

namespace MvsAppApi.Core.Structs
{
    public class HmqlValue
    {
        [JsonProperty(PropertyName = "value")]
        public string Value { get; set; }
        [JsonProperty(PropertyName = "type")]
        public string Type { get; set; }
    }
}