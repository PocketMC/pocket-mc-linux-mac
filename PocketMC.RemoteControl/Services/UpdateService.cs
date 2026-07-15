using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace PocketMC.RemoteControl.Services;

public class UpdateService
{
    private readonly HttpClient _httpClient;

    public UpdateService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PocketMC-UpdateService/1.0");
    }

    public async Task<UpdateInfo?> CheckForUpdatesAsync(bool useBetaChannel = false)
    {
        // Mock GitHub releases API check
        var url = useBetaChannel 
            ? "https://api.github.com/repos/pocketmc/pocketmc/releases" 
            : "https://api.github.com/repos/pocketmc/pocketmc/releases/latest";

        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                // Note: Simplified for demonstration
                // Real implementation would parse JSON and compare Semantic Versions
                return new UpdateInfo
                {
                    Version = "v1.1.0",
                    ReleaseNotes = "## New Features\n- Remote Dashboard\n- Playit Integration\n- Bug Fixes",
                    IsMandatory = false,
                    DownloadUrl = "https://github.com/pocketmc/pocketmc/releases/latest"
                };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UpdateService] Update check failed: {ex.Message}");
        }

        return null;
    }

    public async Task DownloadAndApplyUpdateAsync(UpdateInfo updateInfo, Action<double> progressCallback)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // MacOS: Call Sparkle via P/Invoke (Mocked here)
            Console.WriteLine("Triggering Sparkle SUUpdater...");
            progressCallback(100);
            await Task.Delay(500); // Simulate Sparkle init
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Linux: AppImageUpdate zsync (Mocked download progress)
            Console.WriteLine("Starting AppImageUpdate zsync...");
            for (int i = 0; i <= 100; i += 10)
            {
                progressCallback(i);
                await Task.Delay(200);
            }
        }
        else
        {
            // Fallback
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = updateInfo.DownloadUrl,
                UseShellExecute = true
            });
            progressCallback(100);
        }
    }
}

public class UpdateInfo
{
    public string Version { get; set; } = "";
    public string ReleaseNotes { get; set; } = "";
    public bool IsMandatory { get; set; }
    public string DownloadUrl { get; set; } = "";
}
