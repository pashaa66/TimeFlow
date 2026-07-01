using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace TimeFlow
{
    public static class JsonStore
    {
        public static JsonElement Load(string path, string passphrase = null)
        {
            if (!File.Exists(path)) return default;
            string raw = File.ReadAllText(path, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(raw)) return default;
            using var doc = JsonDocument.Parse(raw);
            var obj = doc.RootElement;

            if (obj.ValueKind != JsonValueKind.Object)
                return obj.Clone();

            if (obj.TryGetProperty("enc", out var enc) && enc.GetBoolean() && obj.TryGetProperty("payload", out var payload))
            {
                string key = passphrase ?? Crypto.DefaultPassphrase;
                string plain = Crypto.DecryptText(payload.GetString(), key);
                return JsonDocument.Parse(plain).RootElement.Clone();
            }
            return obj.Clone();
        }

        public static void Save(string path, object data, bool encrypt, string passphrase = null)
        {
            var opts = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            string json = JsonSerializer.Serialize(data, opts);

            if (encrypt)
            {
                string key = passphrase ?? Crypto.DefaultPassphrase;
                string payload = Crypto.EncryptText(json, key);
                var container = new Dictionary<string, object> { ["enc"] = true, ["payload"] = payload };
                json = JsonSerializer.Serialize(container, opts);
            }
            File.WriteAllText(path, json, Encoding.UTF8);
        }

        public static bool IsEncrypted(string path)
        {
            if (!File.Exists(path)) return false;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
                return doc.RootElement.TryGetProperty("enc", out var enc) && enc.GetBoolean();
            }
            catch { return false; }
        }
    }
}
