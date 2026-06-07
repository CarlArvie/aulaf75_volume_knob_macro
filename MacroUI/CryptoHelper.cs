using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Security.Principal;

namespace MacroUI
{
    public static class CryptoHelper
    {
        private static readonly byte[] Key;

        static CryptoHelper()
        {
            // Derive a machine-bound key
            string machineName = Environment.MachineName;
            string userName = Environment.UserName;
            string sid = WindowsIdentity.GetCurrent()?.User?.Value ?? "UnknownSID";

            string rawKey = $"{machineName}_{userName}_{sid}_AulaMacro";
            using (var sha256 = SHA256.Create())
            {
                Key = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawKey));
            }
        }

        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;

            using (var aes = Aes.Create())
            {
                aes.Key = Key;
                aes.GenerateIV();

                using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                using (var ms = new MemoryStream())
                {
                    ms.Write(aes.IV, 0, aes.IV.Length); // Prepend IV
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    using (var sw = new StreamWriter(cs))
                    {
                        sw.Write(plainText);
                    }
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        public static string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;

            try
            {
                byte[] fullCipher = Convert.FromBase64String(cipherText);

                using (var aes = Aes.Create())
                {
                    aes.Key = Key;
                    byte[] iv = new byte[aes.BlockSize / 8];
                    Array.Copy(fullCipher, 0, iv, 0, iv.Length);
                    aes.IV = iv;

                    using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                    using (var ms = new MemoryStream(fullCipher, iv.Length, fullCipher.Length - iv.Length))
                    using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    using (var sr = new StreamReader(cs))
                    {
                        return sr.ReadToEnd();
                    }
                }
            }
            catch
            {
                // If decryption fails (e.g. wrong machine, not a base64 string), return raw
                return cipherText;
            }
        }
    }
}
