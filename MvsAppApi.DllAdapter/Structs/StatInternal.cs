using System;
using System.Runtime.InteropServices;

namespace MvsAppApi.DllAdapter.Structs
{
    // having problems with Categories/Flags which are both defined as in the C++ as "const char **" ...
    // see https://www.codeproject.com/Articles/17450/Marshal-an-Array-of-Zero-Terminated-Strings-or-Str (maybe, but yuk!)
    // [StructLayout(LayoutKind.Sequential)] // this is default anyways
    struct StatInternal
    {
        public int StructSize;
        public string Name;
        public int TableType;
        public string Value;
        public string Description;
        public string DetailedDescription;
        public string Title;
        public int Width;
        public string Format;
        //[MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.LPTStr)] // doesn't help
        //public string[] Categories;
        public IntPtr Categories;
        public int CategoriesCount;
        //[MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.LPTStr)] // doesn't help
        //public string[] Flags;
        public IntPtr Flags;
        public int FlagsCount;
    }
}