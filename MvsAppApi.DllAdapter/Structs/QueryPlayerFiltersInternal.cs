using System.Runtime.InteropServices;
using MvsAppApi.Core.Enums;

namespace MvsAppApi.DllAdapter.Structs
{
    // todo: eliminate use of 'unsafe' and fixed length arrays
    internal unsafe struct QueryPlayerFiltersInternal
    {
        public int StructSize;
        public int SiteId;
        public int HandsMin;
        public int HandsMax;
        public int CashHandsMin;
        public int CashHandsMax;
        public int TourneyHandsMin;
        public int TourneyHandsMax;
        public int Anon;
        public int TableType;
        public int GameType;
        public int LimitTo;
        public fixed int OrderBy[5]; // max of 5 filters
        public bool OrderByDesc;
        public int Offset;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string PlayerName;
    }
}