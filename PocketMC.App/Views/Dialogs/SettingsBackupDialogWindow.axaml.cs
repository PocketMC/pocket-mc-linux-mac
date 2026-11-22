using System;
using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using Lucide.Avalonia;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;

namespace PocketMC.App.Views.Dialogs;

public enum SettingsBackupMode
{
    Export,
    Import
}

public partial class SettingsCategoryItem : ObservableObject
{
    public string CategoryKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public LucideIconKind IconKind { get; set; } = LucideIconKind.SlidersHorizontal;
    public bool IsSensitive { get; set; }

    [ObservableProperty]
    private bool _isSelected = true;
}

public partial class SettingsBackupDialogWindow : Window
{
    private readonly ISettingsService _settingsService;
    private readonly SettingsBackupService _backupService;
    private readonly SettingsBackupMode _mode;
    private SettingsBackupPackage? _importedPackage;

    public ObservableCollection<SettingsCategoryItem> Categories { get; } = new();

    public SettingsBackupDialogWindow()
    {
        InitializeComponent();
        _settingsService = null!;
        _backupService = new SettingsBackupService();
    }

    public SettingsBackupDialogWindow(
        ISettingsService settingsService,
        SettingsBackupService backupService,
        SettingsBackupMode mode = SettingsBackupMode.Export,
        SettingsBackupPackage? importedPackage = null) : this()
    {
        _settingsService = settingsService;
        _backupService = backupService;
        _mode = mode;
        _importedPackage = importedPackage;

        InitializeCategories();

        if (_mode == SettingsBackupMode.Export)
        {
            TxtTitle.Text = "Export Settings Backup";
            TxtSubtitle.Text = "Select configuration categories and credentials to include in the backup file.";
            BtnAction.Content = "Export Backup...";
            HeaderIcon.Kind = LucideIconKind.Download;
        }
        else
        {
            TxtTitle.Text = "Import Settings Backup";
            TxtSubtitle.Text = "Review and choose which categories to restore into your current configuration.";
            BtnAction.Content = "Restore Settings";
            HeaderIcon.Kind = LucideIconKind.Upload;
        }

        CategoriesList.ItemsSource = Categories;
    }

    private void InitializeCategories()
    {
        Categories.Add(new SettingsCategoryItem
        {
            CategoryKey = "AppBehavior",
            Title = "Application Behavior",
            Description = "Startup, tray minimization, sleep prevention, console history buffer.",
            IconKind = LucideIconKind.SlidersHorizontal,
            IsSelected = true
        });

        Categories.Add(new SettingsCategoryItem
        {
            CategoryKey = "Appearance",
            Title = "Appearance & Backdrop",
            Description = "Theme, custom accent color, wallpaper backdrop, blur radius & tint.",
            IconKind = LucideIconKind.Palette,
            IsSelected = true
        });

        Categories.Add(new SettingsCategoryItem
        {
            CategoryKey = "Notifications",
            Title = "Desktop Notifications",
            Description = "Server online, playit agent, AI summary, and remote control alerts.",
            IconKind = LucideIconKind.Bell,
            IsSelected = true
        });

        Categories.Add(new SettingsCategoryItem
        {
            CategoryKey = "AiConfiguration",
            Title = "AI Diagnostician & Models",
            Description = "AI provider settings, selected model names, and custom endpoints.",
            IconKind = LucideIconKind.Sparkles,
            IsSelected = true
        });

        Categories.Add(new SettingsCategoryItem
        {
            CategoryKey = "AiApiKeys",
            Title = "AI Provider API Keys",
            Description = "Saved API keys for Gemini, OpenAI, Anthropic, or Ollama Cloud.",
            IconKind = LucideIconKind.Key,
            IsSensitive = true,
            IsSelected = true
        });

        Categories.Add(new SettingsCategoryItem
        {
            CategoryKey = "CurseForge",
            Title = "CurseForge API Key",
            Description = "Personal API key for downloading Minecraft mods and modpacks.",
            IconKind = LucideIconKind.Flame,
            IsSensitive = true,
            IsSelected = true
        });

        Categories.Add(new SettingsCategoryItem
        {
            CategoryKey = "Discord",
            Title = "Discord Rich Presence",
            Description = "Discord status presence toggles, bot configuration.",
            IconKind = LucideIconKind.MessageSquare,
            IsSelected = true
        });

        Categories.Add(new SettingsCategoryItem
        {
            CategoryKey = "CloudBackups",
            Title = "Cloud Backups & Tokens",
            Description = "Google Drive, OneDrive, and Dropbox OAuth synchronization credentials.",
            IconKind = LucideIconKind.Cloud,
            IsSensitive = true,
            IsSelected = true
        });

        Categories.Add(new SettingsCategoryItem
        {
            CategoryKey = "RemoteControl",
            Title = "Remote Control Web Panel",
            Description = "Port, binding address, authenticated administrator account, and sessions.",
            IconKind = LucideIconKind.Smartphone,
            IsSensitive = true,
            IsSelected = true
        });
    }

    private void SelectAll_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var item in Categories) item.IsSelected = true;
    }

    private void DeselectAll_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var item in Categories) item.IsSelected = false;
    }

    private void BtnCancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private async void BtnAction_Click(object? sender, RoutedEventArgs e)
    {
        var selectedCategories = BuildSelectedCategories();

        if (_mode == SettingsBackupMode.Export)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider == null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save PocketMC Settings Backup",
                DefaultExtension = "json",
                SuggestedFileName = $"pocketmc-settings-backup-{DateTime.UtcNow:yyyyMMdd}.json"
            });

            if (file != null)
            {
                var package = _backupService.CreateBackupPackage(_settingsService.Settings, selectedCategories);
                _backupService.ExportToFile(file.Path.LocalPath, package);
                Close(true);
            }
        }
        else if (_mode == SettingsBackupMode.Import && _importedPackage != null)
        {
            _backupService.ApplyBackupPackage(_settingsService.Settings, _importedPackage, selectedCategories);
            _settingsService.Save();
            Close(true);
        }
    }

    private SettingsBackupCategories BuildSelectedCategories()
    {
        var cat = new SettingsBackupCategories();
        foreach (var item in Categories)
        {
            switch (item.CategoryKey)
            {
                case "AppBehavior": cat.IncludeAppBehavior = item.IsSelected; break;
                case "Appearance": cat.IncludeAppearance = item.IsSelected; break;
                case "Notifications": cat.IncludeNotifications = item.IsSelected; break;
                case "AiConfiguration": cat.IncludeAiConfiguration = item.IsSelected; break;
                case "AiApiKeys": cat.IncludeAiApiKeys = item.IsSelected; break;
                case "CurseForge": cat.IncludeCurseForgeApiKey = item.IsSelected; break;
                case "Discord": cat.IncludeDiscord = item.IsSelected; break;
                case "CloudBackups": cat.IncludeCloudBackups = item.IsSelected; break;
                case "RemoteControl": cat.IncludeRemoteControl = item.IsSelected; break;
            }
        }
        return cat;
    }
}
