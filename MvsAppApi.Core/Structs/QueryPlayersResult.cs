using System.Collections.Concurrent;

namespace MvsAppApi.Core.Structs
{
    public class QueryPlayersResult : CallbackResult
    {
        public BlockingCollection<PlayerData> Players { get; set; }
    }
}