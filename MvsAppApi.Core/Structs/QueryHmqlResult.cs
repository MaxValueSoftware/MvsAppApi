using System.Collections.Concurrent;

namespace MvsAppApi.Core.Structs
{
    public class QueryHmqlResult : CallbackResult
    {
        public BlockingCollection<HmqlValue[]> Values { get; set; }
    }
}