using System;
using System.Collections.Generic;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using PocketMC.Core.Services;

namespace PocketMC.Platform.Services
{
    public class AesFallbackSecretStore : ISecretStore
    {
        private readonly string _secretsFilePath;
        private readonly string _machineIdFilePath;
        private readonly byte[] _key;
        private static readonly byte[] Salt = Encoding.UTF8.GetBytes("PocketMC_System_Salt_2026!");

        public AesFallbackSecretStore(ISettingsService settingsService)
        {
            var configDir = settingsService.GetSettingsDirectory();
            _secretsFilePath = Path.Combine(configDir, "secrets.json");
            _machineIdFilePath = Path.Combine(configDir, ".machine-id");
            _key = DeriveKey();
        }

        private byte[] DeriveKey()
        {
            string machineId = GetMachineId();
            string macAddress = GetMacAddress();
            string combined = $"{machineId}:{macAddress}";

            using (var pbkdf2 = new Rfc2898DeriveBytes(combined, Salt, 10000, HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(32); // AES-256
            }
        }

        private string GetMachineId()
        {
            if (File.Exists(_machineIdFilePath))
            {
                return File.ReadAllText(_machineIdFilePath).Trim();
            }

            string id = "";
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    if (File.Exists("/etc/machine-id"))
                        id = File.ReadAllText("/etc/machine-id").Trim();
                    else if (File.Exists("/var/lib/dbus/machine-id"))
                        id = File.ReadAllText("/var/lib/dbus/machine-id").Trim();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    // Running macOS command safely
                    using (var process = new System.Diagnostics.Process())
                    {
                        process.StartInfo.FileName = "/usr/sbin/ioreg";
                        process.StartInfo.Arguments = "-rd1 -c IOPlatformExpertDevice";
                        process.StartInfo.RedirectStandardOutput = true;
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.CreateNoWindow = true;
                        process.Start();
                        string output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();

                        // Parse UUID
                        int idx = output.IndexOf("IOPlatformUUID");
                        if (idx != -1)
                        {
                            int start = output.IndexOf("\"", idx);
                            if (start != -1)
                            {
                                int end = output.IndexOf("\"", start + 1);
                                if (end != -1)
                                {
                                    id = output.Substring(start + 1, end - start - 1);
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Fall through
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                id = Guid.NewGuid().ToString();
            }

            try
            {
                File.WriteAllText(_machineIdFilePath, id);
            }
            catch
            {
                // In case of write protection
            }

            return id;
        }

        private string GetMacAddress()
        {
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == OperationalStatus.Up && 
                        ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    {
                        var address = ni.GetPhysicalAddress().ToString();
                        if (!string.IsNullOrEmpty(address))
                            return address;
                    }
                }
            }
            catch
            {
                // Fall through
            }
            return "00:00:00:00:00:00";
        }

        private Dictionary<string, string> LoadSecrets()
        {
            if (!File.Exists(_secretsFilePath))
                return new Dictionary<string, string>();

            try
            {
                var json = File.ReadAllText(_secretsFilePath);
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        }

        private void SaveSecrets(Dictionary<string, string> secrets)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(secrets, options);
                File.WriteAllText(_secretsFilePath, json);
            }
            catch
            {
                // Fall through
            }
        }

        public Task<string?> GetAsync(string key)
        {
            var secrets = LoadSecrets();
            if (!secrets.TryGetValue(key, out string? encryptedBase64) || encryptedBase64 == null)
                return Task.FromResult<string?>(null);

            try
            {
                byte[] raw = Convert.FromBase64String(encryptedBase64);
                byte[] iv = new byte[16];
                byte[] cipherBytes = new byte[raw.Length - 16];
                Buffer.BlockCopy(raw, 0, iv, 0, 16);
                Buffer.BlockCopy(raw, 16, cipherBytes, 0, cipherBytes.Length);

                using (var aes = Aes.Create())
                {
                    aes.Key = _key;
                    aes.IV = iv;
                    using (var decryptor = aes.CreateDecryptor())
                    using (var ms = new MemoryStream(cipherBytes))
                    using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    using (var sr = new StreamReader(cs))
                    {
                        return Task.FromResult<string?>(sr.ReadToEnd());
                    }
                }
            }
            catch
            {
                return Task.FromResult<string?>(null);
            }
        }

        public Task SetAsync(string key, string value)
        {
            var secrets = LoadSecrets();

            using (var aes = Aes.Create())
            {
                aes.Key = _key;
                aes.GenerateIV();
                byte[] iv = aes.IV;

                using (var encryptor = aes.CreateEncryptor())
                using (var ms = new MemoryStream())
                {
                    // Write IV first
                    ms.Write(iv, 0, iv.Length);

                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    using (var sw = new StreamWriter(cs))
                    {
                        sw.Write(value);
                    }

                    byte[] encrypted = ms.ToArray();
                    secrets[key] = Convert.ToBase64String(encrypted);
                }
            }

            SaveSecrets(secrets);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string key)
        {
            var secrets = LoadSecrets();
            if (secrets.Remove(key))
            {
                SaveSecrets(secrets);
            }
            return Task.CompletedTask;
        }
    }
}
