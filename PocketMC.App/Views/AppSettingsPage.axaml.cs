using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using PocketMC.App.ViewModels;
using PocketMC.App.Views.Dialogs;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;

namespace PocketMC.App.Views;

public partial class AppSettingsPage : UserControl
{
    public AppSettingsPage()
    {
        InitializeComponent();
    }

    public AppSettingsPage(AppSettingsViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private async void BtnExportBackup_Click(object? sender, RoutedEventArgs e)
    {
        if (App.Services == null) return;

        var topLevel = TopLevel.GetTopLevel(this) as Window;
        if (topLevel == null) return;

        var settingsService = App.Services.GetRequiredService<ISettingsService>();
        var backupService = App.Services.GetRequiredService<SettingsBackupService>();

        var dialog = new SettingsBackupDialogWindow(settingsService, backupService, SettingsBackupMode.Export);
        await dialog.ShowDialog(topLevel);
    }

    private async void BtnRestoreBackup_Click(object? sender, RoutedEventArgs e)
    {
        if (App.Services == null) return;

        var topLevel = TopLevel.GetTopLevel(this) as Window;
        if (topLevel == null) return;

        var storage = topLevel.StorageProvider;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select PocketMC Settings Backup",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("JSON Backup (*.json)") { Patterns = new[] { "*.json" } }
            }
        });

        if (files.Count == 0) return;

        var settingsService = App.Services.GetRequiredService<ISettingsService>();
        var backupService = App.Services.GetRequiredService<SettingsBackupService>();

        try
        {
            var package = backupService.ImportFromFile(files[0].Path.LocalPath);
            var dialog = new SettingsBackupDialogWindow(settingsService, backupService, SettingsBackupMode.Import, package);
            bool? restored = await dialog.ShowDialog<bool>(topLevel);

            if (restored == true && DataContext is AppSettingsViewModel vm)
            {
                vm.LoadFromSettings();
            }
        }
        catch (Exception ex)
        {
            var dialogService = App.Services.GetService<IDialogService>();
            if (dialogService != null)
            {
                await dialogService.ShowDialogAsync("Failed to Import", $"Could not read settings backup file: {ex.Message}", DialogType.Error);
            }
        }
    }
}
