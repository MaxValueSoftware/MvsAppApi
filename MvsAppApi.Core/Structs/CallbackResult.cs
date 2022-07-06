using System;

namespace MvsAppApi.Core.Structs
{
    public class CallbackResult
    {
        public int CallerId { get; set; }
        public bool Errored { get; set; }
        public int ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
        public IntPtr UserData { get; set; }
    }
}