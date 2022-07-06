using Newtonsoft.Json;

namespace MvsAppApi.Core.Structs
{
    public class PlayerNote
    {
        [JsonProperty(PropertyName = "player")]
        public string Player { get; set; }
        [JsonProperty(PropertyName = "color")]
        public string Color { get; set; }
        [JsonProperty(PropertyName = "note")]
        public string Note { get; set; }
    }
}