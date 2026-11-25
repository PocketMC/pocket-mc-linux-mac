using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Utils;

namespace PocketMC.RemoteControl.Tunnels;

public sealed record CloudflaredDownloadSpec(string Url, bool IsTarGz);

public class CloudflaredQuickTunnelProvider : IRemoteTunnelProvider
{
    private readonly ISettingsService _settingsService;
    private readonly TunnelProcessGroupManager _processManager;
    private readonly HttpClient _httpClient;
    private string _publicUrl = "";

    public string ProviderId => "cloudflared-quick";
    public string DisplayName => "Cloudflared Quick Tunnel";
    public string PublicUrl => _publicUrl;
    public bool IsRunning => _processManager.IsRunning;

    public CloudflaredQuickTunnelProvider(ISettingsService settingsService)
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

        var port = _settingsService.Settings.RemoteControl.Port;
        if (port == 0) port = 8080;

        var startInfo = new ProcessStartInfo
        {
            FileName = binPath,
            Arguments = $"tunnel --url http://localhost:{port}",
            RedirectStandardOutput = false,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _processManager.StartProcess(startInfo);
        
        var tcs = new TaskCompletionSource<bool>();
        _ = Task.Run(() => 
        {
            try
            {
                var p = Process.GetProcessById(_processManager.Pgid);
                var regex = new Regex(@"https://[a-zA-Z0-9-]+\.trycloudflare\.com");
                var logsDir = _settingsService.GetLogsDirectory();
                Directory.CreateDirectory(logsDir);
                var logFile = Path.Combine(logsDir, "tunnels.log");

                while (!p.StandardError.EndOfStream)
                {
                    var line = p.StandardError.ReadLine();
                    if (line != null)
                    {
                        try { File.AppendAllText(logFile, line + "\n"); } catch {}
                        var match = regex.Match(line);
                        if (match.Success && !tcs.Task.IsCompleted)
                        {
                            _publicUrl = match.Value;
                            tcs.TrySetResult(true);
                        }
                    }
                }
            }
            catch {}
            finally
            {
                tcs.TrySetResult(false);
            }
        });

        var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(15000));
        if (completedTask == tcs.Task)
        {
            return await tcs.Task;
        }
        return false;
    }

    public async Task StopAsync()
    {
        _processManager.StopProcessGroup();
        _publicUrl = "";
        await Task.CompletedTask;
    }

    public Task<string> GetStatusAsync()
    {
        if (_processManager.IsRunning && !string.IsNullOrEmpty(_publicUrl))
        {
            return Task.FromResult($"Running: {_publicUrl}");
        }
        return Task.FromResult("Stopped");
    }

    public static CloudflaredDownloadSpec ResolveDownloadSpec(OSPlatform platform, Architecture arch)
    {
        if (platform == OSPlatform.OSX)
        {
            string url = arch == Architecture.Arm64
                ? "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-darwin-arm64.tgz"
                : "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-darwin-amd64.tgz";
            return new CloudflaredDownloadSpec(url, IsTarGz: true);
        }

        if (platform == OSPlatform.Linux)
        {
            string url = arch == Architecture.Arm64
                ? "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-arm64"
                : "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-amd64";
            return new CloudflaredDownloadSpec(url, IsTarGz: false);
        }

        return new CloudflaredDownloadSpec(string.Empty, IsTarGz: false);
    }

    private async Task<string> DownloadBinaryAsync()
    {
        var binDir = Path.Combine(_settingsService.GetSettingsDirectory(), "bin");
        Directory.CreateDirectory(binDir);

        var platform = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? OSPlatform.OSX
            : (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? OSPlatform.Linux : OSPlatform.Windows);

        var spec = ResolveDownloadSpec(platform, RuntimeInformation.ProcessArchitecture);
        if (string.IsNullOrWhiteSpace(spec.Url))
        {
            return string.Empty;
        }

        string binPath = Path.Combine(binDir, "cloudflared");
        if (!File.Exists(binPath))
        {
            var response = await _httpClient.GetAsync(spec.Url);
            if (!response.IsSuccessStatusCode) return string.Empty;

            if (spec.IsTarGz)
            {
                string tempArchive = Path.Combine(binDir, $"cloudflared-{Guid.NewGuid():N}.tgz");
                string extractDir = Path.Combine(binDir, $"cf-extract-{Guid.NewGuid():N}");
                try
                {
                    byte[] bytes = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(tempArchive, bytes);
                    SafeZipExtractor.ExtractTarGz(tempArchive, extractDir);

                    string? extractedBin = Directory.EnumerateFiles(extractDir, "cloudflared", SearchOption.AllDirectories)
                        .FirstOrDefault();
                    if (string.IsNullOrWhiteSpace(extractedBin) || !File.Exists(extractedBin))
                    {
                        return string.Empty;
                    }

                    File.Move(extractedBin, binPath, overwrite: true);
                }
                finally
                {
                    try { if (File.Exists(tempArchive)) File.Delete(tempArchive); } catch { }
                    try { if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true); } catch { }
                }
            }
            else
            {
                byte[] bytes = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(binPath, bytes);
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && File.Exists(binPath))
            {
                File.SetUnixFileMode(binPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }
        }
        return binPath;
    }
}
