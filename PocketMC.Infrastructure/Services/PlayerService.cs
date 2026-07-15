using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PocketMC.Core.Models;
using PocketMC.Core.Services;

namespace PocketMC.Infrastructure.Services
{
    public class PlayerService : IPlayerService, IDisposable
    {
        private readonly ISettingsService _settingsService;
        private readonly IConsoleLogService _logService;
        private readonly HttpClient _httpClient;
        private readonly ConcurrentDictionary<string, string> _uuidCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, HashSet<string>> _onlinePlayers = new();
        private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers = new();
        private readonly string _uuidCacheFilePath;
        private readonly object _cacheLock = new();

        // Regex patterns for player join/leave logs
        private static readonly Regex JavaJoinRegex = new(@"\[Server thread/INFO\]: (\w+) joined the game", RegexOptions.Compiled);
        private static readonly Regex JavaLeaveRegex = new(@"\[Server thread/INFO\]: (\w+) left the game", RegexOptions.Compiled);
        private static readonly Regex BedrockJoinRegex = new(@"Player connected: (\w+)", RegexOptions.Compiled);
        private static readonly Regex BedrockLeaveRegex = new(@"Player disconnected: (\w+)", RegexOptions.Compiled);
        private static readonly Regex PocketMineJoinRegex = new(@"\[Server thread/INFO\]: (\w+)\[/.*\] logged in", RegexOptions.Compiled);
        private static readonly Regex PocketMineLeaveRegex = new(@"\[Server thread/INFO\]: (\w+) left the game", RegexOptions.Compiled);
        private static readonly Regex AnsiRegex = new(@"\x1B\[[0-9;]*[a-zA-Z]", RegexOptions.Compiled);

        public PlayerService(
            ISettingsService settingsService,
            IConsoleLogService logService)
        {
            _settingsService = settingsService;
            _logService = logService;
            _httpClient = new HttpClient();
            _uuidCacheFilePath = Path.Combine(_settingsService.GetCacheDirectory(), "player_uuids.json");

            LoadUuidCache();
            _logService.LogReceived += HandleLogLine;
        }

        private void LoadUuidCache()
        {
            lock (_cacheLock)
            {
                if (File.Exists(_uuidCacheFilePath))
                {
                    try
                    {
                        var json = File.ReadAllText(_uuidCacheFilePath);
                        var dictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                        if (dictionary != null)
                        {
                            foreach (var kvp in dictionary)
                            {
                                _uuidCache[kvp.Key] = kvp.Value;
                            }
                        }
                    }
                    catch
                    {
                        // Cache load failed, start fresh
                    }
                }
            }
        }

        private void SaveUuidCache()
        {
            lock (_cacheLock)
            {
                try
                {
                    var json = JsonSerializer.Serialize(_uuidCache, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_uuidCacheFilePath, json);
                }
                catch
                {
                    // Fail silently
                }
            }
        }

        private async Task<string> ResolveUuidAsync(string username)
        {
            if (_uuidCache.TryGetValue(username, out var cached))
            {
                return cached;
            }

            try
            {
                var response = await _httpClient.GetFromJsonAsync<MojangProfile>($"https://api.mojang.com/users/profiles/minecraft/{username}");
                if (response != null && !string.IsNullOrEmpty(response.Id))
                {
                    var formatted = FormatUUID(response.Id);
                    _uuidCache[username] = formatted;
                    SaveUuidCache();
                    return formatted;
                }
            }
            catch
            {
                // Fallback to offline UUID generation if Mojang API lookup fails or times out
            }

            // Fallback offline UUID algorithm (standard Java MD5 of OfflinePlayer:username)
            var offlineUuid = GenerateOfflineUUID(username);
            _uuidCache[username] = offlineUuid;
            SaveUuidCache();
            return offlineUuid;
        }

        private string FormatUUID(string rawId)
        {
            if (rawId.Length != 32) return rawId;
            return $"{rawId.Substring(0, 8)}-{rawId.Substring(8, 4)}-{rawId.Substring(12, 4)}-{rawId.Substring(16, 4)}-{rawId.Substring(20)}";
        }

        private string GenerateOfflineUUID(string username)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes("OfflinePlayer:" + username));
                // Set variant to IETF RFC 4122
                hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
                // Set version to 3 (name-based MD5)
                hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
                return new Guid(hash).ToString();
            }
        }

        private class MojangProfile
        {
            public string Name { get; set; } = string.Empty;
            public string Id { get; set; } = string.Empty;
        }

        public async Task<List<string>> GetOpsAsync(ServerInstance instance)
        {
            if (instance.EngineType == EngineType.PocketMine)
            {
                var filePath = Path.Combine(instance.Path, "ops.txt");
                return ReadTxtLines(filePath);
            }
            else if (instance.EngineType == EngineType.Bedrock)
            {
                var filePath = Path.Combine(instance.Path, "permissions.json");
                if (!File.Exists(filePath)) return new List<string>();
                try
                {
                    var json = await File.ReadAllTextAsync(filePath);
                    var list = JsonSerializer.Deserialize<List<BedrockPermission>>(json);
                    return list?.Select(p => p.Xuid).ToList() ?? new List<string>();
                }
                catch
                {
                    return new List<string>();
                }
            }
            else // Java
            {
                var filePath = Path.Combine(instance.Path, "ops.json");
                if (!File.Exists(filePath)) return new List<string>();
                try
                {
                    var json = await File.ReadAllTextAsync(filePath);
                    var list = JsonSerializer.Deserialize<List<JavaPlayerEntry>>(json);
                    return list?.Select(p => p.Name).ToList() ?? new List<string>();
                }
                catch
                {
                    return new List<string>();
                }
            }
        }

        public async Task AddOpAsync(ServerInstance instance, string username)
        {
            if (instance.EngineType == EngineType.PocketMine)
            {
                var filePath = Path.Combine(instance.Path, "ops.txt");
                AddTxtLine(filePath, username);
            }
            else if (instance.EngineType == EngineType.Bedrock)
            {
                var filePath = Path.Combine(instance.Path, "permissions.json");
                var list = new List<BedrockPermission>();
                if (File.Exists(filePath))
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(filePath);
                        list = JsonSerializer.Deserialize<List<BedrockPermission>>(json) ?? new List<BedrockPermission>();
                    }
                    catch { }
                }

                // BDS uses XUID, for this v1 mock we use a hash-derived 16-digit XUID or mock-lookup
                var xuid = GenerateNumericId(username).ToString();
                if (!list.Any(p => p.Xuid == xuid))
                {
                    list.Add(new BedrockPermission { Xuid = xuid, Permission = "operator" });
                    var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                    await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(list, jsonOptions));
                }
            }
            else // Java
            {
                var filePath = Path.Combine(instance.Path, "ops.json");
                var list = new List<JavaPlayerEntry>();
                if (File.Exists(filePath))
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(filePath);
                        list = JsonSerializer.Deserialize<List<JavaPlayerEntry>>(json) ?? new List<JavaPlayerEntry>();
                    }
                    catch { }
                }

                if (!list.Any(p => p.Name.Equals(username, StringComparison.OrdinalIgnoreCase)))
                {
                    var uuid = await ResolveUuidAsync(username);
                    list.Add(new JavaPlayerEntry { Uuid = uuid, Name = username, Level = 4, BypassesPlayerLimit = false });
                    var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                    await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(list, jsonOptions));
                }
            }
        }

        public async Task RemoveOpAsync(ServerInstance instance, string username)
        {
            if (instance.EngineType == EngineType.PocketMine)
            {
                var filePath = Path.Combine(instance.Path, "ops.txt");
                RemoveTxtLine(filePath, username);
            }
            else if (instance.EngineType == EngineType.Bedrock)
            {
                var filePath = Path.Combine(instance.Path, "permissions.json");
                if (File.Exists(filePath))
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(filePath);
                        var list = JsonSerializer.Deserialize<List<BedrockPermission>>(json) ?? new List<BedrockPermission>();
                        var xuid = GenerateNumericId(username).ToString();
                        var item = list.FirstOrDefault(p => p.Xuid == xuid);
                        if (item != null)
                        {
                            list.Remove(item);
                            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                            await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(list, jsonOptions));
                        }
                    }
                    catch { }
                }
            }
            else // Java
            {
                var filePath = Path.Combine(instance.Path, "ops.json");
                if (File.Exists(filePath))
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(filePath);
                        var list = JsonSerializer.Deserialize<List<JavaPlayerEntry>>(json) ?? new List<JavaPlayerEntry>();
                        var item = list.FirstOrDefault(p => p.Name.Equals(username, StringComparison.OrdinalIgnoreCase));
                        if (item != null)
                        {
                            list.Remove(item);
                            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                            await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(list, jsonOptions));
                        }
                    }
                    catch { }
                }
            }
        }

        public async Task<List<string>> GetWhitelistAsync(ServerInstance instance)
        {
            if (instance.EngineType == EngineType.PocketMine)
            {
                var filePath = Path.Combine(instance.Path, "white-list.txt");
                return ReadTxtLines(filePath);
            }
            else if (instance.EngineType == EngineType.Bedrock)
            {
                var filePath = Path.Combine(instance.Path, "whitelist.json");
                if (!File.Exists(filePath)) return new List<string>();
                try
                {
                    var json = await File.ReadAllTextAsync(filePath);
                    var list = JsonSerializer.Deserialize<List<BedrockWhitelistEntry>>(json);
                    return list?.Select(p => p.Name).ToList() ?? new List<string>();
                }
                catch
                {
                    return new List<string>();
                }
            }
            else // Java
            {
                var filePath = Path.Combine(instance.Path, "whitelist.json");
                if (!File.Exists(filePath)) return new List<string>();
                try
                {
                    var json = await File.ReadAllTextAsync(filePath);
                    var list = JsonSerializer.Deserialize<List<JavaPlayerEntry>>(json);
                    return list?.Select(p => p.Name).ToList() ?? new List<string>();
                }
                catch
                {
                    return new List<string>();
                }
            }
        }

        public async Task AddWhitelistAsync(ServerInstance instance, string username)
        {
            if (instance.EngineType == EngineType.PocketMine)
            {
                var filePath = Path.Combine(instance.Path, "white-list.txt");
                AddTxtLine(filePath, username);
            }
            else if (instance.EngineType == EngineType.Bedrock)
            {
                var filePath = Path.Combine(instance.Path, "whitelist.json");
                var list = new List<BedrockWhitelistEntry>();
                if (File.Exists(filePath))
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(filePath);
                        list = JsonSerializer.Deserialize<List<BedrockWhitelistEntry>>(json) ?? new List<BedrockWhitelistEntry>();
                    }
                    catch { }
                }

                if (!list.Any(p => p.Name.Equals(username, StringComparison.OrdinalIgnoreCase)))
                {
                    var xuid = GenerateNumericId(username).ToString();
                    list.Add(new BedrockWhitelistEntry { Xuid = xuid, Name = username, IgnoresPlayerLimit = false });
                    var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                    await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(list, jsonOptions));
                }
            }
            else // Java
            {
                var filePath = Path.Combine(instance.Path, "whitelist.json");
                var list = new List<JavaPlayerEntry>();
                if (File.Exists(filePath))
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(filePath);
                        list = JsonSerializer.Deserialize<List<JavaPlayerEntry>>(json) ?? new List<JavaPlayerEntry>();
                    }
                    catch { }
                }

                if (!list.Any(p => p.Name.Equals(username, StringComparison.OrdinalIgnoreCase)))
                {
                    var uuid = await ResolveUuidAsync(username);
                    list.Add(new JavaPlayerEntry { Uuid = uuid, Name = username });
                    var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                    await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(list, jsonOptions));
                }
            }
        }

        public async Task RemoveWhitelistAsync(ServerInstance instance, string username)
        {
            if (instance.EngineType == EngineType.PocketMine)
            {
                var filePath = Path.Combine(instance.Path, "white-list.txt");
                RemoveTxtLine(filePath, username);
            }
            else if (instance.EngineType == EngineType.Bedrock)
            {
                var filePath = Path.Combine(instance.Path, "whitelist.json");
                if (File.Exists(filePath))
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(filePath);
                        var list = JsonSerializer.Deserialize<List<BedrockWhitelistEntry>>(json) ?? new List<BedrockWhitelistEntry>();
                        var item = list.FirstOrDefault(p => p.Name.Equals(username, StringComparison.OrdinalIgnoreCase));
                        if (item != null)
                        {
                            list.Remove(item);
                            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                            await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(list, jsonOptions));
                        }
                    }
                    catch { }
                }
            }
            else // Java
            {
                var filePath = Path.Combine(instance.Path, "whitelist.json");
                if (File.Exists(filePath))
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(filePath);
                        var list = JsonSerializer.Deserialize<List<JavaPlayerEntry>>(json) ?? new List<JavaPlayerEntry>();
                        var item = list.FirstOrDefault(p => p.Name.Equals(username, StringComparison.OrdinalIgnoreCase));
                        if (item != null)
                        {
                            list.Remove(item);
                            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                            await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(list, jsonOptions));
                        }
                    }
                    catch { }
                }
            }
        }

        public Task<List<string>> GetOnlinePlayersAsync(ServerInstance instance)
        {
            var list = _onlinePlayers.GetOrAdd(instance.Slug, _ => new HashSet<string>());
            lock (list)
            {
                return Task.FromResult(list.ToList());
            }
        }

        private void HandleLogLine(string slug, string line)
        {
            var cleanLine = AnsiRegex.Replace(line, string.Empty);
            var joinSet = _onlinePlayers.GetOrAdd(slug, _ => new HashSet<string>());

            // Check Java patterns
            var match = JavaJoinRegex.Match(cleanLine);
            if (match.Success)
            {
                lock (joinSet) { joinSet.Add(match.Groups[1].Value); }
                return;
            }
            match = JavaLeaveRegex.Match(cleanLine);
            if (match.Success)
            {
                lock (joinSet) { joinSet.Remove(match.Groups[1].Value); }
                return;
            }

            // Check Bedrock patterns
            match = BedrockJoinRegex.Match(cleanLine);
            if (match.Success)
            {
                lock (joinSet) { joinSet.Add(match.Groups[1].Value); }
                return;
            }
            match = BedrockLeaveRegex.Match(cleanLine);
            if (match.Success)
            {
                lock (joinSet) { joinSet.Remove(match.Groups[1].Value); }
                return;
            }

            // Check PocketMine patterns
            match = PocketMineJoinRegex.Match(cleanLine);
            if (match.Success)
            {
                lock (joinSet) { joinSet.Add(match.Groups[1].Value); }
                return;
            }
            match = PocketMineLeaveRegex.Match(cleanLine);
            if (match.Success)
            {
                lock (joinSet) { joinSet.Remove(match.Groups[1].Value); }
                return;
            }
        }

        private List<string> ReadTxtLines(string path)
        {
            if (!File.Exists(path)) return new List<string>();
            try
            {
                return File.ReadAllLines(path)
                    .Select(l => l.Trim())
                    .Where(l => !string.IsNullOrEmpty(l))
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        private void AddTxtLine(string path, string line)
        {
            try
            {
                var lines = ReadTxtLines(path);
                if (!lines.Contains(line, StringComparer.OrdinalIgnoreCase))
                {
                    lines.Add(line);
                    File.WriteAllLines(path, lines);
                }
            }
            catch { }
        }

        private void RemoveTxtLine(string path, string line)
        {
            try
            {
                var lines = ReadTxtLines(path);
                var beforeCount = lines.Count;
                lines = lines.Where(l => !l.Equals(line, StringComparison.OrdinalIgnoreCase)).ToList();
                if (lines.Count < beforeCount)
                {
                    File.WriteAllLines(path, lines);
                }
            }
            catch { }
        }

        private long GenerateNumericId(string input)
        {
            long hash = 17;
            foreach (char c in input)
            {
                hash = hash * 31 + c;
            }
            return Math.Abs(hash);
        }

        public void Dispose()
        {
            _logService.LogReceived -= HandleLogLine;
            _httpClient.Dispose();
            foreach (var w in _watchers.Values)
            {
                w.Dispose();
            }
        }

        // Data models for serialization
        private class JavaPlayerEntry
        {
            public string Uuid { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public int? Level { get; set; }
            public bool? BypassesPlayerLimit { get; set; }
        }

        private class BedrockPermission
        {
            public string Xuid { get; set; } = string.Empty;
            public string Permission { get; set; } = string.Empty;
        }

        private class BedrockWhitelistEntry
        {
            public string Xuid { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public bool IgnoresPlayerLimit { get; set; }
        }
    }
}
