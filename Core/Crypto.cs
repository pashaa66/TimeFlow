using System;
using System.Security.Cryptography;
using System.Text;

namespace TimeFlow
{
    public static class Crypto
    {
        public static string Encrypt(byte[] data, string passphrase)
        {
            byte[] cipher = Rc4(data, passphrase);
            return Convert.ToBase64String(cipher);
        }

        public static byte[] Decrypt(string base64, string passphrase)
        {
            byte[] cipher = Convert.FromBase64String(base64);
            return Rc4(cipher, passphrase);
        }

        public static string EncryptText(string text, string passphrase)
            => Encrypt(Encoding.UTF8.GetBytes(text), passphrase);

        public static string DecryptText(string base64, string passphrase)
        {
            byte[] raw = Decrypt(base64, passphrase);
            return Encoding.UTF8.GetString(raw);
        }

        public static string DefaultPassphrase => "TimeFlow::v2::localDataKey::7f3a91c5e2";

        private static byte[] Rc4(byte[] data, string passphrase)
        {
            byte[] key;
            using (var sha = SHA256.Create())
                key = sha.ComputeHash(Encoding.UTF8.GetBytes(passphrase));

            byte[] s = new byte[256];
            for (int i = 0; i < 256; i++) s[i] = (byte)i;
            int j = 0;
            for (int i = 0; i < 256; i++)
            {
                j = (j + s[i] + key[i % key.Length]) & 0xFF;
                (s[i], s[j]) = (s[j], s[i]);
            }

            byte[] outb = new byte[data.Length];
            int x = 0, y = 0;
            for (int n = 0; n < data.Length; n++)
            {
                x = (x + 1) & 0xFF;
                y = (y + s[x]) & 0xFF;
                (s[x], s[y]) = (s[y], s[x]);
                byte k = s[(s[x] + s[y]) & 0xFF];
                outb[n] = (byte)(data[n] ^ k);
            }
            return outb;
        }
    }
}
