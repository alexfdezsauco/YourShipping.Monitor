using System.Security.Cryptography;
using System.Text;

namespace YourShipping.Monitor.Server.Extensions
{
    public static class StringExtensions
    {
        public static string ComputeSha256(this string content)
        {
            using (var hashAlgorithm = SHA256.Create())
            {
                var data = hashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(content));
                var stringBuilder = new StringBuilder();
                for (var i = 0; i < data.Length; i++)
                {
                    stringBuilder.Append(data[i].ToString("x2"));
                }

                return stringBuilder.ToString();
            }
        }
    }
}