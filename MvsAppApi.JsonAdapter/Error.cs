using Newtonsoft.Json;

namespace MvsAppApi.JsonAdapter
{
    public class Error
    {
        [JsonProperty(PropertyName = "code")] 
        public ErrorCode ErrorCode { get; set; }
        
        [JsonProperty(PropertyName = "message")]
        public string Message { get; set; }
    }
}