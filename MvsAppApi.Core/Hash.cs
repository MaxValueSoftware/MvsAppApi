using System;
using System.Security.Cryptography;
using System.Text;

namespace MvsAppApi.Core
{
    public class Hash
    {
        public static string Calculate(HashAlgorithm hashAlgorithm, string appId, string salt)
        {
            // step 1, calculate hash from input
            var inputBytes = Encoding.ASCII.GetBytes(appId + ":" + salt);
            var hash = hashAlgorithm.ComputeHash(inputBytes);

            // for SHA512 hashes, use base64 instead of hex encoding so its consistent with what PT4 uses (for now, hm3 will support both) ...
            if (hashAlgorithm is SHA512)
                return Convert.ToBase64String(hash);

            // step 2, convert byte array to hex string
            var sb = new StringBuilder();
            foreach (var t in hash)
                sb.Append(t.ToString("X2"));
            return sb.ToString();
        }
    }
}