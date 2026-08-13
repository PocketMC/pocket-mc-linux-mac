using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using PocketMC.Core.Services;

namespace PocketMC.Infrastructure.Services
{
    public class UpdateService : IUpdateService
    {
        private readonly string _currentVersion;
        private readonly HttpClient _httpClient;
        private const string LatestReleaseUrl = "https://api.github.com/repos/PocketMC/pocket-mc-linux-mac/releases/latest";

        public UpdateService(string currentVersion, HttpClient? httpClient = null)
        {
            _currentVersion = (currentVersion ?? "1.0.0.0").TrimStart('v');
            _httpClient = httpClient ?? new HttpClient();
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "PocketMC-UpdateChecker");
            }
        }

        public async Task<UpdateCheckResult> CheckForUpdatesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync(LatestReleaseUrl);
                if (!response.IsSuccessStatusCode)
                {
                    return new UpdateCheckResult { IsUpdateAvailable = false, CurrentVersion = _currentVersion };
                }

                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                string tagName = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? "" : "";
                string htmlUrl = root.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() ?? "" : "";

                string remoteVersionStr = tagName.TrimStart('v');
                bool isNewer = false;

                if (Version.TryParse(remoteVersionStr, out var remoteVer) && Version.TryParse(_currentVersion, out var localVer))
                {
                    isNewer = remoteVer > localVer;
                }
                else if (!string.IsNullOrEmpty(remoteVersionStr))
                {
                    isNewer = string.Compare(remoteVersionStr, _currentVersion, StringComparison.OrdinalIgnoreCase) > 0;
                }

                return new UpdateCheckResult
                {
                    IsUpdateAvailable = isNewer,
                    CurrentVersion = _currentVersion,
                    LatestVersionTag = tagName,
                    ReleaseUrl = htmlUrl
                };
            }
            catch
            {
                return new UpdateCheckResult { IsUpdateAvailable = false, CurrentVersion = _currentVersion };
            }
        }
    }
}
