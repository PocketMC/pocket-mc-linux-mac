using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using PocketMC.Core.Services;

namespace PocketMC.RemoteControl.Tunnels;

public class PlayitHttpsTunnelProvider : IRemoteTunnelProvider
{
    private readonly ISettingsService _settingsService;
    private readonly TunnelProcessGroupManager _processManager;
    private readonly HttpClient _httpClient;

    public string ProviderId => "playit-https";
    public string DisplayName => "Playit.gg HTTPS Tunnel";

    public PlayitHttpsTunnelProvider(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _processManager = new TunnelProcessGroupManager();
        _httpClient = new HttpClient();
    }

    public async Task<bool> StartAsync()
    {
        if (_processManager.IsRunning) return true;

        var binPath = await DownloadBinaryAsync();
        if (string.IsNullOrEmpty(binPath)) return false;

        var secretPath = Path.Combine(_settingsService.GetSettingsDirectory(), "playit-secret.txt");
        var secret = _settingsService.Settings.PlayitPartnerConnection?.SecretKey;
        if (!string.IsNullOrEmpty(secret))
        {
            await File.WriteAllTextAsync(secretPath, secret);
        }

        var socketPath = Path.Combine(_settingsService.GetSettingsDirectory(), "playit.sock");
        if (File.Exists(socketPath))
        {
            try { File.Delete(socketPath); } catch {}
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = binPath,
            Arguments = $"--secret-path \"{secretPath}\" --socket-path \"{socketPath}\"",
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _processManager.StartProcess(startInfo);
        
        return true;
    }

    public async Task StopAsync()
    {
        _processManager.StopProcessGroup();
        await Task.CompletedTask;
    }

    public Task<string> GetStatusAsync()
    {
        return Task.FromResult(_processManager.IsRunning ? "Running" : "Stopped");
    }

    private async Task<string> DownloadBinaryAsync()
    {
        var binDir = Path.Combine(_settingsService.GetSettingsDirectory(), "bin");
        Directory.CreateDirectory(binDir);
        
        string fileName = "playit";
        string url = "";
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            url = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 
                ? "https://github.com/playit-cloud/playit-agent/releases/latest/download/playit-linux-aarch64"
                : "https://github.com/playit-cloud/playit-agent/releases/latest/download/playit-linux-amd64";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            url = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 
                ? "https://github.com/playit-cloud/playit-agent/releases/latest/download/playit-macos-aarch64"
                : "https://github.com/playit-cloud/playit-agent/releases/latest/download/playit-macos-amd64";
        }
        else
        {
            return ""; // Not supported
        }

        var binPath = Path.Combine(binDir, fileName);
        if (!File.Exists(binPath))
        {
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return "";
            
            var bytes = await response.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(binPath, bytes);
            
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                File.SetUnixFileMode(binPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }
        }
        return binPath;
    }
}
