using System.Collections.Concurrent;

namespace MvsAppApi.Core.Structs
{
    public class QueryStatsResult : CallbackResult
    {
        public BlockingCollection<StatValue[]> PlayerStatValues { get; set; }
    }
}