using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PocketMC.Core.Services;

namespace PocketMC.App.ViewModels
{
    public partial class RootDirectorySetupViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;

        public event Action? SetupCompleted;

        [ObservableProperty]
        private string _selectedPath = string.Empty;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _isError;

        public RootDirectorySetupViewModel(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            _selectedPath = _settingsService.GetDataRoot();
            if (string.IsNullOrWhiteSpace(_selectedPath))
            {
                _selectedPath = _settingsService.GetDefaultDataRoot();
            }
        }

        [RelayCommand]
        private async Task BrowseFolderAsync()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
            {
                var topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
                if (topLevel != null)
                {
                    var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                    {
                        Title = "Select PocketMC Storage Root Directory",
                        AllowMultiple = false
                    });

                    if (folders != null && folders.Count > 0)
                    {
                        SelectedPath = folders[0].Path.LocalPath;
                        IsError = false;
                        StatusMessage = string.Empty;
                    }
                }
            }
        }

        [RelayCommand]
        private void UseDefault()
        {
            SelectedPath = _settingsService.GetDefaultDataRoot();
            IsError = false;
            StatusMessage = string.Empty;
        }

        [RelayCommand]
        private void Confirm()
        {
            if (string.IsNullOrWhiteSpace(SelectedPath))
            {
                IsError = true;
                StatusMessage = "Please choose a valid directory path.";
                return;
            }

            try
            {
                Directory.CreateDirectory(SelectedPath);
                _settingsService.SetDataRoot(SelectedPath);
                SetupCompleted?.Invoke();
            }
            catch (Exception ex)
            {
                IsError = true;
                StatusMessage = $"Cannot access directory: {ex.Message}";
            }
        }
    }
}
