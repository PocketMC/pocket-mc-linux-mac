using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PocketMC.Core.Models;
using PocketMC.Core.Services;

namespace PocketMC.App.ViewModels.Settings
{
    public partial class AddonItemViewModel : ObservableObject
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public bool IsPlugin { get; set; }

        [ObservableProperty]
        private bool _isEnabled = true;
    }

    public partial class SettingsAddonsVM : ObservableObject
    {
        private string _serverDir = string.Empty;
        private readonly IDialogService _dialogService;
        private readonly Action _markDirty;

        [ObservableProperty] private bool _autoUpdateAddons = true;
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private AddonItemViewModel? _selectedAddon;

        public ObservableCollection<AddonItemViewModel> Plugins { get; } = new();
        public ObservableCollection<AddonItemViewModel> Mods { get; } = new();

        public IRelayCommand RefreshCommand { get; }
        public IRelayCommand OpenFolderCommand { get; }
        public IAsyncRelayCommand<AddonItemViewModel> ToggleAddonCommand { get; }
        public IAsyncRelayCommand<AddonItemViewModel> DeleteAddonCommand { get; }

        public SettingsAddonsVM(IDialogService dialogService, Action markDirty)
        {
            _dialogService = dialogService;
            _markDirty = markDirty;

            RefreshCommand = new RelayCommand(LoadAddons);
            OpenFolderCommand = new RelayCommand(OpenFolder);
            ToggleAddonCommand = new AsyncRelayCommand<AddonItemViewModel>(ToggleAddonAsync);
            DeleteAddonCommand = new AsyncRelayCommand<AddonItemViewModel>(DeleteAddonAsync);
        }

        public void UpdateServerDir(string newDir)
        {
            _serverDir = newDir;
            LoadAddons();
        }

        public void LoadAddons()
        {
            Plugins.Clear();
            Mods.Clear();

            if (string.IsNullOrEmpty(_serverDir) || !Directory.Exists(_serverDir)) return;

            // Load Plugins
            string pluginsDir = Path.Combine(_serverDir, "plugins");
            if (Directory.Exists(pluginsDir))
            {
                var files = Directory.GetFiles(pluginsDir, "*.*")
                    .Where(f => f.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase));

                foreach (var f in files)
                {
                    var fi = new FileInfo(f);
                    bool enabled = !f.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
                    string name = fi.Name.Replace(".jar.disabled", "").Replace(".jar", "");
                    
                    if (string.IsNullOrEmpty(SearchText) || name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                    {
                        Plugins.Add(new AddonItemViewModel
                        {
                            FileName = fi.Name,
                            FilePath = fi.FullName,
                            DisplayName = name,
                            IsEnabled = enabled,
                            IsPlugin = true
                        });
                    }
                }
            }

            // Load Mods
            string modsDir = Path.Combine(_serverDir, "mods");
            if (Directory.Exists(modsDir))
            {
                var files = Directory.GetFiles(modsDir, "*.*")
                    .Where(f => f.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase));

                foreach (var f in files)
                {
                    var fi = new FileInfo(f);
                    bool enabled = !f.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
                    string name = fi.Name.Replace(".jar.disabled", "").Replace(".jar", "");

                    if (string.IsNullOrEmpty(SearchText) || name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                    {
                        Mods.Add(new AddonItemViewModel
                        {
                            FileName = fi.Name,
                            FilePath = fi.FullName,
                            DisplayName = name,
                            IsEnabled = enabled,
                            IsPlugin = false
                        });
                    }
                }
            }
        }

        private async Task ToggleAddonAsync(AddonItemViewModel? item)
        {
            if (item == null || !File.Exists(item.FilePath)) return;

            try
            {
                string newPath;
                if (item.IsEnabled)
                {
                    // Disable it -> append .disabled
                    newPath = item.FilePath + ".disabled";
                }
                else
                {
                    // Enable it -> strip .disabled
                    newPath = item.FilePath.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
                        ? item.FilePath.Substring(0, item.FilePath.Length - ".disabled".Length)
                        : item.FilePath;
                }

                File.Move(item.FilePath, newPath, true);
                item.FilePath = newPath;
                item.FileName = Path.GetFileName(newPath);
                item.IsEnabled = !item.IsEnabled;
                _markDirty();
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage("Failed to toggle addon", ex.Message, DialogType.Error);
            }
        }

        private async Task DeleteAddonAsync(AddonItemViewModel? item)
        {
            if (item == null) return;

            var confirm = await _dialogService.ShowDialogAsync(
                "Delete Addon",
                $"Are you sure you want to delete '{item.DisplayName}'?",
                DialogType.Question,
                showCancel: true);

            if (confirm != DialogResult.Ok && confirm != DialogResult.Yes) return;

            try
            {
                if (File.Exists(item.FilePath))
                {
                    File.Delete(item.FilePath);
                }
                LoadAddons();
                _markDirty();
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage("Delete Failed", ex.Message, DialogType.Error);
            }
        }

        private void OpenFolder()
        {
            if (string.IsNullOrEmpty(_serverDir)) return;

            string targetDir = Path.Combine(_serverDir, "plugins");
            if (!Directory.Exists(targetDir))
            {
                targetDir = Path.Combine(_serverDir, "mods");
            }
            if (!Directory.Exists(targetDir))
            {
                targetDir = _serverDir;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = targetDir,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        partial void OnAutoUpdateAddonsChanged(bool value) => _markDirty();
        partial void OnSearchTextChanged(string value) => LoadAddons();
    }
}
