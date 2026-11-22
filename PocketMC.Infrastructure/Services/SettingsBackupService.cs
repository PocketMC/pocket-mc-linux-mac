using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using PocketMC.Core.Models;

namespace PocketMC.Infrastructure.Services;

public class SettingsBackupService
{
    private const int CurrentBackupVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public SettingsBackupPackage CreateBackupPackage(
        Settings settings,
        SettingsBackupCategories categories,
        string? appVersion = null)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        if (categories == null) throw new ArgumentNullException(nameof(categories));

        var package = new SettingsBackupPackage
        {
            Version = CurrentBackupVersion,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            AppVersion = appVersion ?? "1.0.0",
            IncludedCategories = categories
        };

        if (categories.IncludeAppBehavior)
        {
            package.AppBehavior = new AppBehaviorBackupData
            {
                StartMinimizedToTray = settings.StartMinimizedToTray,
                MinimizeToTrayOnClose = settings.MinimizeToTrayOnClose,
                KeepComputerAwakeWhileServersRunning = settings.KeepComputerAwakeWhileServersRunning,
                ConsoleBufferSize = settings.ConsoleBufferSize,
                LastSeenChangelogVersion = settings.LastSeenChangelogVersion,
                HasCompletedInitialSetup = settings.HasCompletedInitialSetup
            };
        }

        if (categories.IncludeAppearance)
        {
            package.Appearance = new AppearanceBackupData
            {
                WindowBackdrop = settings.WindowBackdrop,
                AccentColorMode = settings.AccentColorMode,
                CustomAccentColor = settings.CustomAccentColor,
                CustomBackgroundImagePath = settings.CustomBackgroundImagePath,
                WallpaperBlurRadius = settings.WallpaperBlurRadius,
                WallpaperTintOpacity = settings.WallpaperTintOpacity
            };
        }

        if (categories.IncludeStoragePaths)
        {
            package.StoragePaths = new StoragePathsBackupData
            {
                CustomDataRoot = settings.CustomDataRoot,
                ExternalBackupDirectory = settings.ExternalBackupDirectory
            };
        }

        if (categories.IncludeNotifications)
        {
            package.Notifications = new NotificationsBackupData
            {
                EnableServerOnlineNotifications = settings.EnableServerOnlineNotifications,
                EnableAgentConnectNotifications = settings.EnableAgentConnectNotifications,
                EnableRemoteControlNotifications = settings.EnableRemoteControlNotifications,
                EnableAiSummaryNotifications = settings.EnableAiSummaryNotifications,
                EnableDiscordNotifications = settings.EnableDiscordNotifications
            };
        }

        if (categories.IncludeAiConfiguration || categories.IncludeAiApiKeys)
        {
            var aiData = new AiBackupData();
            if (categories.IncludeAiConfiguration)
            {
                aiData.AiProvider = settings.AiProvider;
                aiData.EnableAiSummarization = settings.EnableAiSummarization;
                foreach (var kvp in settings.AiModels) aiData.AiModels[kvp.Key] = kvp.Value;
                foreach (var kvp in settings.AiEndpoints) aiData.AiEndpoints[kvp.Key] = kvp.Value;
            }
            if (categories.IncludeAiApiKeys)
            {
                foreach (var kvp in settings.AiApiKeys) aiData.AiApiKeys[kvp.Key] = kvp.Value;
            }
            package.AiConfiguration = aiData;
        }

        if (categories.IncludeCurseForgeApiKey)
        {
            package.CurseForge = new CurseForgeBackupData
            {
                CurseForgeApiKey = settings.CurseForgeApiKey
            };
        }

        if (categories.IncludeDiscord)
        {
            package.Discord = new DiscordBackupData
            {
                EnableDiscordRpc = settings.EnableDiscordRpc,
                DiscordRpc = settings.DiscordRpc,
                DiscordUserId = settings.DiscordUserId,
                DiscordApiUrl = settings.DiscordApiUrl,
                DiscordApiKey = settings.DiscordApiKey
            };
        }

        if (categories.IncludePlayitTunnel && settings.PlayitPartnerConnection != null)
        {
            package.PlayitTunnel = settings.PlayitPartnerConnection;
        }

        if (categories.IncludeCloudBackups)
        {
            package.CloudBackups = new CloudBackupsBackupData
            {
                CloudBackups = settings.CloudBackups,
                CloudTokens = settings.CloudTokens
            };
        }

        if (categories.IncludeRemoteControl)
        {
            package.RemoteControl = settings.RemoteControl;
        }

        return package;
    }

    public void ApplyBackupPackage(
        Settings target,
        SettingsBackupPackage package,
        SettingsBackupCategories? overrideCategories = null)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (package == null) throw new ArgumentNullException(nameof(package));

        var categories = overrideCategories ?? package.IncludedCategories ?? new SettingsBackupCategories();

        if (categories.IncludeAppBehavior && package.AppBehavior != null)
        {
            target.StartMinimizedToTray = package.AppBehavior.StartMinimizedToTray;
            target.MinimizeToTrayOnClose = package.AppBehavior.MinimizeToTrayOnClose;
            target.KeepComputerAwakeWhileServersRunning = package.AppBehavior.KeepComputerAwakeWhileServersRunning;
            target.ConsoleBufferSize = package.AppBehavior.ConsoleBufferSize;
            if (package.AppBehavior.LastSeenChangelogVersion != null)
                target.LastSeenChangelogVersion = package.AppBehavior.LastSeenChangelogVersion;
        }

        if (categories.IncludeAppearance && package.Appearance != null)
        {
            target.WindowBackdrop = package.Appearance.WindowBackdrop;
            target.AccentColorMode = package.Appearance.AccentColorMode;
            target.CustomAccentColor = package.Appearance.CustomAccentColor;
            target.CustomBackgroundImagePath = package.Appearance.CustomBackgroundImagePath;
            target.WallpaperBlurRadius = package.Appearance.WallpaperBlurRadius;
            target.WallpaperTintOpacity = package.Appearance.WallpaperTintOpacity;
        }

        if (categories.IncludeStoragePaths && package.StoragePaths != null)
        {
            if (!string.IsNullOrWhiteSpace(package.StoragePaths.CustomDataRoot))
                target.CustomDataRoot = package.StoragePaths.CustomDataRoot;
            if (!string.IsNullOrWhiteSpace(package.StoragePaths.ExternalBackupDirectory))
                target.ExternalBackupDirectory = package.StoragePaths.ExternalBackupDirectory;
        }

        if (categories.IncludeNotifications && package.Notifications != null)
        {
            target.EnableServerOnlineNotifications = package.Notifications.EnableServerOnlineNotifications;
            target.EnableAgentConnectNotifications = package.Notifications.EnableAgentConnectNotifications;
            target.EnableRemoteControlNotifications = package.Notifications.EnableRemoteControlNotifications;
            target.EnableAiSummaryNotifications = package.Notifications.EnableAiSummaryNotifications;
            target.EnableDiscordNotifications = package.Notifications.EnableDiscordNotifications;
        }

        if (package.AiConfiguration != null)
        {
            if (categories.IncludeAiConfiguration)
            {
                target.AiProvider = package.AiConfiguration.AiProvider;
                target.EnableAiSummarization = package.AiConfiguration.EnableAiSummarization;
                foreach (var kvp in package.AiConfiguration.AiModels) target.AiModels[kvp.Key] = kvp.Value;
                foreach (var kvp in package.AiConfiguration.AiEndpoints) target.AiEndpoints[kvp.Key] = kvp.Value;
            }
            if (categories.IncludeAiApiKeys)
            {
                foreach (var kvp in package.AiConfiguration.AiApiKeys) target.AiApiKeys[kvp.Key] = kvp.Value;
            }
        }

        if (categories.IncludeCurseForgeApiKey && package.CurseForge != null)
        {
            target.CurseForgeApiKey = package.CurseForge.CurseForgeApiKey;
        }

        if (categories.IncludeDiscord && package.Discord != null)
        {
            target.EnableDiscordRpc = package.Discord.EnableDiscordRpc;
            if (package.Discord.DiscordRpc != null) target.DiscordRpc = package.Discord.DiscordRpc;
            target.DiscordUserId = package.Discord.DiscordUserId;
            target.DiscordApiUrl = package.Discord.DiscordApiUrl;
            target.DiscordApiKey = package.Discord.DiscordApiKey;
        }

        if (categories.IncludePlayitTunnel && package.PlayitTunnel != null)
        {
            target.PlayitPartnerConnection = package.PlayitTunnel;
        }

        if (categories.IncludeCloudBackups && package.CloudBackups != null)
        {
            target.CloudBackups = package.CloudBackups.CloudBackups;
            target.CloudTokens = package.CloudBackups.CloudTokens;
        }

        if (categories.IncludeRemoteControl && package.RemoteControl != null)
        {
            target.RemoteControl = package.RemoteControl;
        }
    }

    public string ExportToJson(SettingsBackupPackage package)
    {
        return JsonSerializer.Serialize(package, JsonOptions);
    }

    public SettingsBackupPackage ImportFromJson(string json)
    {
        return JsonSerializer.Deserialize<SettingsBackupPackage>(json, JsonOptions)
               ?? throw new InvalidOperationException("Failed to deserialize settings backup package.");
    }

    public void ExportToFile(string filePath, SettingsBackupPackage package)
    {
        string json = ExportToJson(package);
        File.WriteAllText(filePath, json);
    }

    public SettingsBackupPackage ImportFromFile(string filePath)
    {
        string json = File.ReadAllText(filePath);
        return ImportFromJson(json);
    }
}
