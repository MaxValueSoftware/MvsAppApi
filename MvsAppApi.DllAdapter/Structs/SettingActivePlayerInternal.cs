using System.Runtime.InteropServices;

namespace MvsAppApi.DllAdapter.Structs
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct SettingActivePlayerInternal
    {
        public int StructSize;
        public int SiteId;
        [MarshalAs(UnmanagedType.LPStr)] 
        public string PlayerName;
    }
}