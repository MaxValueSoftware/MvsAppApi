using System.Collections.Generic;
using Newtonsoft.Json;

namespace MvsAppApi.JsonAdapter
{
    public class Request
    {
        [JsonProperty(PropertyName = "id")]
        public int Id { get; set; }

        [JsonProperty(PropertyName = "method")]
        public string Method { get; set; }

        [JsonProperty(PropertyName = "params")]
        public Dictionary<string, object> Params { get; set; }

        [JsonProperty(PropertyName = "result", NullValueHandling = NullValueHandling.Ignore)]
        public object Result { get; set; }

        [JsonProperty(PropertyName = "error", NullValueHandling = NullValueHandling.Ignore)]
        public object Error { get; set; }

        // transient (identifies which server pipe is being used)
        public int Index { get; set; }
    }
}