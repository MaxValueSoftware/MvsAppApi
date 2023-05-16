using System;
using System.Runtime.InteropServices;
using System.Text;
using MvsAppApi.Core;

namespace MvsAppApi.DllAdapter
{
    // see https://stackoverflow.com/questions/42443175/marshal-const-char
    // also, revising this to mirror functionality in .net standard 2.1
    public unsafe static class MvsMarshal
    {
        // .net standard 2.1 equivalent funtions
        public static string PtrToStringUTF8(IntPtr utf8)
        {
            if (utf8 == null)
                return null;
            var length = GetStringLength((byte*)utf8);
            var result = length == 0
                ? string.Empty
                : Encoding.UTF8.GetString((byte*)utf8, length);
            return result;
        }

        public static IntPtr StringToCoTaskMemUTF8(string s)
        {
            if (s is null)
                return IntPtr.Zero;

            int nb = Encoding.UTF8.GetMaxByteCount(s.Length);
            IntPtr pMem = Marshal.AllocCoTaskMem(nb + 1);

            int nbWritten;
            byte* pbMem = (byte*)pMem;

            fixed (char* firstChar = s)
            {
                nbWritten = Encoding.UTF8.GetBytes(firstChar, s.Length, pbMem, nb);
            }

            pbMem[nbWritten] = 0;
            return pMem;
        }

        // array helpers

        public static string[] PtrArrToStringArrUTF8(byte** nativeStrings, int stringCount, LogCallback _log)
        {
            var strings = new string[stringCount];
            for (var x = 0; x < stringCount; ++x)
                strings[x] = PtrToStringUTF8((IntPtr)nativeStrings[x]);
            return strings;
        }

        // alternate approach that uses IntPtr for byte ** (see SelectStats .. we'd change all the other callback signatures if we want to use this)
        public static string[] PtrToStringArrUTF8(IntPtr nativeStringsPtr, int stringCount)
        {
            var nativeStrings = (byte**)nativeStringsPtr;
            var strings = new string[stringCount];
            for (var x = 0; x < stringCount; ++x)
                strings[x] = PtrToStringUTF8((IntPtr)nativeStrings[x]);
            return strings;
        }

        public static IntPtr StringArrToCoTaskMemUTF8(string[] strings, LogCallback _log)
        {
            var size = sizeof(IntPtr);
            var arr = Marshal.AllocCoTaskMem((strings.Length + 1) * size);
            int x = 0;
            byte** byteArr = (byte**)arr;
            foreach (var str in strings)
            {
                var ptr = StringToCoTaskMemUTF8(str);
                _log($@"str={str}, x={x}, ptr={ptr}");
                byteArr[x] = (byte *)ptr.ToPointer();
                x++;
            }
            byteArr[x] = (byte *) IntPtr.Zero.ToPointer();
            _log($@"done: x={x}, ptr={(IntPtr) byteArr[x]}, IntPtr.Zero={IntPtr.Zero}");

            // iterate over them as a test
            byteArr = (byte**)arr;
            x = 0;
            for (var ptr = byteArr[x]; ptr != IntPtr.Zero.ToPointer(); x++)
                _log($@"x={x}, ptr={(IntPtr)ptr}");
            _log($@"done");

            return arr;
        }

        public static void FreeCoTaskMemStringArr(IntPtr arr, LogCallback _log)
        {
            return; // todo: fix infinite loop caused by casting of byte* to IntPtr (its not finding the terminating IntPtr.Zero)
            var byteArr = (byte **) arr;
            
            var x = 0;
            for (var ptr = byteArr[x]; ptr != (byte*)IntPtr.Zero; x++)
            {
                _log($@"x={x}, ptr={(IntPtr)ptr}, IntPtr.Zero={IntPtr.Zero}");
                Marshal.FreeCoTaskMem((IntPtr)ptr);
            }
            _log($@"done");
            Marshal.FreeCoTaskMem(arr);
        }

        // private helpers
        private static int GetStringLength(byte* nativeString)
        {
            var length = 0;

            while (*nativeString != '\0')
            {
                ++length;
                ++nativeString;
            }

            return length;
        }
    }
}