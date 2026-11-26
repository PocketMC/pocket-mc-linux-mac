using System;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PocketMC.Core.Services;

namespace PocketMC.Infrastructure.WhatsNew
{
    public sealed class WhatsNewService
    {
        private readonly ISettingsService _settingsService;
        private readonly ILogger<WhatsNewService> _logger;
        private readonly string _currentVersion;

        public WhatsNewService(
            ISettingsService settingsService,
            string? currentVersion = null,
            ILogger<WhatsNewService>? logger = null)
        {
            _settingsService = settingsService;
            _logger = logger ?? NullLogger<WhatsNewService>.Instance;
            _currentVersion = !string.IsNullOrWhiteSpace(currentVersion)
                ? currentVersion
                : (!string.IsNullOrWhiteSpace(Configuration.AppConfig.AppVersion)
                    ? Configuration.AppConfig.AppVersion
                    : typeof(WhatsNewService).Assembly.GetName().Version?.ToString(3) ?? "1.0.0");
        }

        public string GetCurrentVersion() => _currentVersion;

        public bool ShouldShow()
        {
            string currentVersion = GetCurrentVersion();
            string? lastSeen = _settingsService.Settings.LastSeenChangelogVersion;

            bool shouldShow = !string.Equals(currentVersion, lastSeen, StringComparison.OrdinalIgnoreCase);
            if (shouldShow)
            {
                _logger.LogInformation("What's New check: current={CurrentVersion}, lastSeen={LastSeen} - showing dialog.", currentVersion, lastSeen ?? "(none)");
            }
            return shouldShow;
        }

        public ChangelogEntry? LoadChangelog(string? customContent = null)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(customContent))
                {
                    return ChangelogParser.Parse(customContent);
                }

                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PocketMC.Infrastructure.WhatsNew.txt");
                if (stream != null)
                {
                    using var reader = new StreamReader(stream);
                    return ChangelogParser.Parse(reader.ReadToEnd());
                }

                // Check app directory fallback
                string appDir = AppContext.BaseDirectory;
                string filePath = Path.Combine(appDir, "WhatsNew.txt");
                if (File.Exists(filePath))
                {
                    return ChangelogParser.Parse(File.ReadAllText(filePath));
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load WhatsNew changelog.");
                return null;
            }
        }

        public void MarkAsSeen()
        {
            try
            {
                string currentVersion = GetCurrentVersion();
                _settingsService.Settings.LastSeenChangelogVersion = currentVersion;
                _settingsService.Save();
                _logger.LogInformation("What's New marked as seen for version {Version}.", currentVersion);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist LastSeenChangelogVersion.");
            }
        }
    }
}
