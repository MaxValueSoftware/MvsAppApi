using Newtonsoft.Json;

namespace MvsAppApi.Core.Structs
{
    public class PlayerData
    {

        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }
        [JsonProperty(PropertyName = "site_id")]
        public int SiteId { get; set; }
        [JsonProperty(PropertyName = "anon")]
        public bool Anon { get; set; }
        [JsonProperty(PropertyName = "c_hands")]
        public int CashHands { get; set; }
        [JsonProperty(PropertyName = "t_hands")]
        public int TournamentHands { get; set; }
    }
}