using Newtonsoft.Json;

namespace MvsAppApi.Core.Structs
{
    public class CurrentPlayerInfo
    {
        [JsonProperty(PropertyName = "player_name")]
        public string PlayerName;
        [JsonProperty(PropertyName = "site_id")]
        public string SiteId;
    }
}