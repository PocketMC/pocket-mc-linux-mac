using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;

namespace PocketMC.App.ViewModels.Settings
{
    public partial class SettingsVersionUpdatesVM : ObservableObject
    {
        private ServerInstance? _instance;
        private readonly IInstanceUpdateService? _updateService;
        private readonly Func<bool>? _isRunningCheck;

        public ObservableCollection<UpdateTargetVersionInfo> AvailableTargetVersions { get; } = new();

        [ObservableProperty] private UpdateTargetVersionInfo? _selectedTargetVersion;
        [ObservableProperty] private bool _isLoadingTargetVersions;
        [ObservableProperty] private bool _isUpdating;
        [ObservableProperty] private double _updateProgress;
        [ObservableProperty] private string _updateStatusText = string.Empty;
        [ObservableProperty] private string _updateErrorText = string.Empty;
        [ObservableProperty] private bool _hasUpdateError;
        [ObservableProperty] private bool _isUpdateSuccess;
        [ObservableProperty] private string _updateRuntimeChangeText = string.Empty;

        public IAsyncRelayCommand LoadTargetVersionsCommand { get; }
        public IAsyncRelayCommand ApplyUpdateCommand { get; }

        public SettingsVersionUpdatesVM(
            IInstanceUpdateService? updateService = null,
            Func<bool>? isRunningCheck = null)
        {
            _updateService = updateService;
            _isRunningCheck = isRunningCheck;

            LoadTargetVersionsCommand = new AsyncRelayCommand(LoadTargetVersionsAsync);
            ApplyUpdateCommand = new AsyncRelayCommand(ApplyUpdateAsync);
        }

        public void SetInstance(ServerInstance instance)
        {
            _instance = instance;
            _ = LoadTargetVersionsAsync();
        }

        public void UpdateServerDir(string newDir)
        {
            // Server directory updated
        }

        partial void OnSelectedTargetVersionChanged(UpdateTargetVersionInfo? value)
        {
            if (value != null && _instance != null)
            {
                if (_instance.EngineType == EngineType.PocketMine)
                {
                    UpdateRuntimeChangeText = "PHP 8.2+ managed runtime";
                }
                else if (_instance.EngineType == EngineType.Bedrock)
                {
                    UpdateRuntimeChangeText = "Native Bedrock Dedicated Server binary";
                }
                else
                {
                    int currJava = InstanceUpdatePlanner.MapMinecraftVersionToJavaVersion(_instance.EngineVersion);
                    int targetJava = InstanceUpdatePlanner.MapMinecraftVersionToJavaVersion(value.Id);
                    UpdateRuntimeChangeText = currJava == targetJava
                        ? $"Java {targetJava} remains compatible"
                        : $"Java runtime requirement changes: Java {currJava} -> Java {targetJava}";
                }
            }
            else
            {
                UpdateRuntimeChangeText = string.Empty;
            }
        }

        public async Task LoadTargetVersionsAsync()
        {
            if (_instance == null || _updateService == null) return;

            IsLoadingTargetVersions = true;
            AvailableTargetVersions.Clear();
            SelectedTargetVersion = null;
            HasUpdateError = false;
            IsUpdateSuccess = false;

            try
            {
                var versions = await _updateService.GetAvailableTargetVersionsAsync(_instance);
                foreach (var v in versions)
                {
                    AvailableTargetVersions.Add(v);
                }

                SelectedTargetVersion = AvailableTargetVersions.FirstOrDefault(v => v.IsUpgrade)
                    ?? AvailableTargetVersions.FirstOrDefault();
            }
            catch (Exception ex)
            {
                HasUpdateError = true;
                UpdateErrorText = $"Failed to check for updates: {ex.Message}";
            }
            finally
            {
                IsLoadingTargetVersions = false;
            }
        }

        public async Task ApplyUpdateAsync()
        {
            if (_instance == null || SelectedTargetVersion == null || _updateService == null || IsUpdating) return;

            if (_isRunningCheck?.Invoke() == true)
            {
                HasUpdateError = true;
                UpdateErrorText = "Please stop the server before applying an update.";
                return;
            }

            IsUpdating = true;
            HasUpdateError = false;
            IsUpdateSuccess = false;
            UpdateProgress = 0;
            UpdateStatusText = $"Starting update to {SelectedTargetVersion.Id}...";

            var progress = new Progress<double>(val => UpdateProgress = val);

            try
            {
                var result = await _updateService.UpdateAsync(
                    _instance,
                    SelectedTargetVersion.Id,
                    progress,
                    status => UpdateStatusText = status);

                if (result.Success)
                {
                    IsUpdateSuccess = true;
                    _instance.EngineVersion = SelectedTargetVersion.Id;
                    UpdateStatusText = $"Successfully upgraded to {result.TargetVersionApplied}!";
                    await LoadTargetVersionsAsync();
                }
                else
                {
                    HasUpdateError = true;
                    UpdateErrorText = result.ErrorMessage ?? "Unknown update failure occurred.";
                    UpdateStatusText = result.RestoredFromRollback
                        ? "Update failed, but original server files were restored safely via rollback journal."
                        : "Update failed. Please check logs.";
                }
            }
            catch (Exception ex)
            {
                HasUpdateError = true;
                UpdateErrorText = ex.Message;
                UpdateStatusText = "Update failed due to an unexpected error.";
            }
            finally
            {
                IsUpdating = false;
            }
        }
    }
}
