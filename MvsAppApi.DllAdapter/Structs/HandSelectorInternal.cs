using System.Runtime.InteropServices;

namespace MvsAppApi.DllAdapter.Structs
{
    internal struct HandSelectorInternal
    {
        public int StructSize;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string HandNo;
        public int SiteId;
        public int Street;
        public int Action;
    }
}