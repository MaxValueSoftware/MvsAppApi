using Newtonsoft.Json;

namespace MvsAppApi.Core.Structs
{
    public class Stat
    {
        [JsonProperty(PropertyName = "name")]
        public string Name;
        [JsonProperty(PropertyName = "table_type")]
        public string TableType;
        [JsonProperty(PropertyName = "value")]
        public string Value;
        [JsonProperty(PropertyName = "description")]
        public string Description;
        [JsonProperty(PropertyName = "detail")]
        public string Detail;
        [JsonProperty(PropertyName = "title")]
        public string Title;
        [JsonProperty(PropertyName = "width")]
        public int Width;
        [JsonProperty(PropertyName = "format")]
        public string Format;
        [JsonProperty(PropertyName = "categories")]
        public string[] Categories;
        [JsonProperty(PropertyName = "flags")]
        public string[] Flags;
    }
}