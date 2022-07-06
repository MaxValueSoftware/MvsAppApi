using System.Runtime.InteropServices;

namespace MvsAppApi.DllAdapter.Structs
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct SettingHudProfilesInternal
    {
        public long StructSize;
        public unsafe byte** Profiles;
        public long ProfilesCount;
    }
}