using System.Runtime.InteropServices;

namespace MvsAppApi.DllAdapter.Structs
{
    internal struct HandInternal
    {
        public int StructSize;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] 
        public string HandNo;
        public int SiteId;
    }
}