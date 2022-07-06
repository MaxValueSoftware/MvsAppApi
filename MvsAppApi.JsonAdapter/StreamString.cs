using System.IO;
using System.Text;

namespace MvsAppApi.JsonAdapter
{
    public class StreamString
    {
        const int MaxBufLen = 16 * 1024 * 1024;

        private readonly Stream _ioStream;
        private readonly byte[] _buf;

        public StreamString(Stream ioStream)
        {
            _ioStream = ioStream;
            _buf = new byte[MaxBufLen];
        }

        public string ReadString()
        {
            var count = _ioStream.Read(_buf, 0, MaxBufLen);
            var s = Encoding.UTF8.GetString(_buf, 0, count);
            return s;
        }

        public int WriteString(string outString)
        {
            var outBuffer = Encoding.UTF8.GetBytes(outString);

            var len = outBuffer.Length;
            _ioStream.Write(outBuffer, 0, len);
            _ioStream.Flush();

            return outBuffer.Length;
        }
    }
}