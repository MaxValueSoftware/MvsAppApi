using System;
using System.Runtime.InteropServices;

namespace MvsAppApi.DllAdapter.Structs
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct SettingHandTagsInternal
    {
        public long StructSize;
        public IntPtr Tags;
        public long TagsCount;
    }
}