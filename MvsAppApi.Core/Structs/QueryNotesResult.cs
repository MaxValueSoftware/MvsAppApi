using System.Collections.Concurrent;

namespace MvsAppApi.Core.Structs
{
    public class QueryNotesResult : CallbackResult
    {
        public BlockingCollection<PlayerNote> PlayerNotes { get; set; }
    }
}