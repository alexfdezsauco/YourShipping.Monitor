namespace YourShipping.Monitor.Tests
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using AngleSharp;
    using AngleSharp.Dom;
    using AngleSharp.Io;
    using AngleSharp.Io.Network;
    using AngleSharp.Js;

    using Jint.Native.Array;

    using NUnit.Framework;

    public class Tests
    {
        [Test]
        public async Task PoC()
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                "user-agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/85.0.4183.83 Safari/537.36 Edg/85.0.564.44");
            var requester = new HttpClientRequester(httpClient);
            var config = Configuration.Default
                .WithRequester(requester)
                .WithDefaultLoader(new LoaderOptions { IsResourceLoadingEnabled = true })
                .WithJs();

            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync("https://www.tuenvio.cu/stores.json").WaitUntilAvailable();

            var content = document.Body.TextContent;

            var regexA = new Regex(@"a\s*=\s*(toNumbers[^)]+\))", RegexOptions.Compiled);
            var regexB = new Regex(@"b\s*=\s*(toNumbers[^)]+\))", RegexOptions.Compiled);
            var regexC = new Regex(@"c\s*=\s*(toNumbers[^)]+\))", RegexOptions.Compiled);

            var regexCall = new Regex(
                @"document\.cookie\s+=\s+""([^=]+)=""\s+[+]\s+toHex\(slowAES\.decrypt\(([^)]+)\)\)",
                RegexOptions.Compiled);

            var toNumbersACall = regexA.Match(content).Groups[1].Value;
            var toNumbersBCall = regexB.Match(content).Groups[1].Value;
            var toNumbersCCall = regexC.Match(content).Groups[1].Value;

            var match = regexCall.Match(content);
            var cookieName = match.Groups[1].Value;
            var parameters = match.Groups[2].Value;

            parameters = parameters.Replace("a", "%A%").Replace("b", "%B%").Replace("c", "%C%");
            parameters = parameters.Replace("%A%", toNumbersACall).Replace("%B%", toNumbersBCall)
                .Replace("%C%", toNumbersCCall);

            var value = document.ExecuteScript($"toHex(slowAES.decrypt({parameters}))");


            Console.WriteLine(value);
        }

        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void Test1()
        {
            /*
	 * Mode of Operation Decryption
	 * cipherIn - Encrypted String as array of bytes
	 * originalsize - The unencrypted string length - required for CBC
	 * mode - mode of type modeOfOperation
	 * key - a number array of length 'size'
	 * size - the bit length of the key
	 * iv - the 128 bit number array Initialization Vector
	 */
            // decrypt: function(cipherIn, mode, key, iv)
            var original = "Here is some data to encrypt!";

            // Create a new instance of the Aes
            // class.  This generates a new key and initialization
            // vector (IV).
            using (var rijndael = Rijndael.Create())
            {
                // key, iv
                var cryptoTransform = rijndael.CreateDecryptor(null, null);

                // cryptoTransform.

                // Encrypt the string to an array of bytes.
                var encrypted = EncryptStringToBytes_Aes(original, rijndael.Key, rijndael.IV);

                // Decrypt the bytes to a string.
                var roundtrip = DecryptStringFromBytes_Aes(encrypted, rijndael.Key, rijndael.IV);

                // Display the original data and the decrypted data.
                Console.WriteLine("Original:   {0}", original);
                Console.WriteLine("Round Trip: {0}", roundtrip);
            }
        }

        private static string DecryptStringFromBytes(byte[] cipherText, byte[] Key, byte[] IV)
        {
            // Check arguments.
            if (cipherText == null || cipherText.Length <= 0)
            {
                throw new ArgumentNullException("cipherText");
            }

            if (Key == null || Key.Length <= 0)
            {
                throw new ArgumentNullException("Key");
            }

            if (IV == null || IV.Length <= 0)
            {
                throw new ArgumentNullException("IV");
            }

            // Declare the string used to hold
            // the decrypted text.
            string plaintext = null;

            // Create an RijndaelManaged object
            // with the specified key and IV.
            using (var rijAlg = new RijndaelManaged())
            {
                rijAlg.Padding = PaddingMode.None;
                rijAlg.Key = Key;
                rijAlg.IV = IV;

                // Create a decryptor to perform the stream transform.
                var decryptor = rijAlg.CreateDecryptor(rijAlg.Key, rijAlg.IV);

                // Create the streams used for decryption.
                using (var msDecrypt = new MemoryStream(cipherText))
                {
                    using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (var srDecrypt = new StreamReader(csDecrypt))
                        {
                            // Read the decrypted bytes from the decrypting stream
                            // and place them in a string.
                            plaintext = srDecrypt.ReadToEnd();
                        }
                    }
                }
            }

            return plaintext;
        }

        private static string DecryptStringFromBytes_Aes(byte[] cipherText, byte[] Key, byte[] IV)
        {
            // Check arguments.
            if (cipherText == null || cipherText.Length <= 0)
            {
                throw new ArgumentNullException("cipherText");
            }

            if (Key == null || Key.Length <= 0)
            {
                throw new ArgumentNullException("Key");
            }

            if (IV == null || IV.Length <= 0)
            {
                throw new ArgumentNullException("IV");
            }

            // Declare the string used to hold
            // the decrypted text.
            string plaintext = null;

            // Create an Aes object
            // with the specified key and IV.
            using (var aesAlg = Aes.Create())
            {
                aesAlg.Key = Key;
                aesAlg.IV = IV;

                // Create a decryptor to perform the stream transform.
                var decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                // Create the streams used for decryption.
                using (var msDecrypt = new MemoryStream(cipherText))
                {
                    using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (var srDecrypt = new StreamReader(csDecrypt))
                        {
                            // Read the decrypted bytes from the decrypting stream
                            // and place them in a string.
                            plaintext = srDecrypt.ReadToEnd();
                        }
                    }
                }
            }

            return plaintext;
        }

        private static byte[] EncryptStringToBytes_Aes(string plainText, byte[] Key, byte[] IV)
        {
            // Check arguments.
            if (plainText == null || plainText.Length <= 0)
            {
                throw new ArgumentNullException("plainText");
            }

            if (Key == null || Key.Length <= 0)
            {
                throw new ArgumentNullException("Key");
            }

            if (IV == null || IV.Length <= 0)
            {
                throw new ArgumentNullException("IV");
            }

            byte[] encrypted;

            // Create an Aes object
            // with the specified key and IV.
            using (var aesAlg = Aes.Create())
            {
                aesAlg.Key = Key;
                aesAlg.IV = IV;

                // Create an encryptor to perform the stream transform.
                var encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                // Create the streams used for encryption.
                using (var msEncrypt = new MemoryStream())
                {
                    using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (var swEncrypt = new StreamWriter(csEncrypt))
                        {
                            // Write all data to the stream.
                            swEncrypt.Write(plainText);
                        }

                        encrypted = msEncrypt.ToArray();
                    }
                }
            }

            // Return the encrypted bytes from the memory stream.
            return encrypted;
        }

        private static byte[] ToByteArray(ArrayInstance arrayInstance)
        {
            var lengthJsValue = arrayInstance.Get("length");
            var length = Convert.ToInt16(lengthJsValue.AsNumber());
            var bytes = new byte[length];
            for (var i = 0; i < length; i++)
            {
                bytes[i] = Convert.ToByte(arrayInstance.Get($"{i}").AsNumber());
            }

            return bytes;
        }
    }

    public static class X
    {
        public static string CallParametersReplace(this string toHexCall, string value, string toNumbersACall)
        {
            var lastIndexOf = toHexCall.LastIndexOf(value);
            return toHexCall.Substring(0, lastIndexOf - 1) + toNumbersACall + toHexCall.Substring(lastIndexOf + 1);
        }
    }
}