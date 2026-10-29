using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using BepInEx;
using Newtonsoft.Json.Linq;

namespace FollowerTalk;

internal sealed class KeyStore
{
    private readonly string path = Path.Combine(Paths.ConfigPath, "FollowerTalk.keys.json");
    private readonly JObject encrypted;

    public KeyStore()
    {
        try
        {
            encrypted = File.Exists(path)
                ? JObject.Parse(File.ReadAllText(path, Encoding.UTF8)) : new JObject();
        }
        catch { encrypted = new JObject(); }
    }

    public string Get(string provider)
    {
        try
        {
            string? value = encrypted[provider]?.Value<string>();
            if (string.IsNullOrEmpty(value)) return "";
            byte[] bytes = ProtectedData.Unprotect(Convert.FromBase64String(value), null,
                DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch { return ""; }
    }

    public bool Save(string provider, string key, out string error)
    {
        try
        {
            if (string.IsNullOrEmpty(key)) encrypted.Remove(provider);
            else
            {
                byte[] bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(key), null,
                    DataProtectionScope.CurrentUser);
                encrypted[provider] = Convert.ToBase64String(bytes);
            }
            File.WriteAllText(path, encrypted.ToString(), Encoding.UTF8);
            error = "";
            return true;
        }
        catch
        {
            error = "Не удалось безопасно сохранить ключ.";
            return false;
        }
    }
}
