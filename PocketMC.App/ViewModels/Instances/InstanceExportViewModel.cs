using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PocketMC.Core.Models;
using PocketMC.Core.Services;

namespace PocketMC.App.ViewModels.Instances
{
    public partial class InstanceExportViewModel : ObservableObject
    {
        private readonly ServerInstance _instance;
        private readonly IInstanceService _instanceService;
        private readonly IDialogService? _dialogService;

        [ObservableProperty] private string _destinationZipPath = string.Empty;
        [ObservableProperty] private bool _includeWorlds = true;
        [ObservableProperty] private bool _includeSensitiveConfiguration;
        [ObservableProperty] private string _currentStep = "Choose export location.";
        [ObservableProperty] private double _overallProgress;
        [ObservableProperty] private bool _isExporting;
        [ObservableProperty] private bool _isComplete;
        [ObservableProperty] private bool _hasError;
        [ObservableProperty] private string? _errorMessage;

        public ServerInstance Instance => _instance;
        public string InstanceName => _instance.Name;
        public string ServerType => _instance.EngineType.ToString();
        public string MinecraftVersion => _instance.EngineVersion;
        public string ServerSummary => $"{ServerType} - {MinecraftVersion}";

        public IAsyncRelayCommand ExportCommand { get; }
        public IAsyncRelayCommand BrowseDestinationCommand { get; }
        public IRelayCommand GoBackCommand { get; }

        public InstanceExportViewModel(
            ServerInstance instance,
            IInstanceService instanceService,
            IDialogService? dialogService = null)
        {
            _instance = instance;
            _instanceService = instanceService;
            _dialogService = dialogService;

            string safeName = string.Join("_", instance.Name.Split(Path.GetInvalidFileNameChars()));
            string defaultDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            DestinationZipPath = Path.Combine(defaultDir, $"{safeName}-export.zip");

            ExportCommand = new AsyncRelayCommand(ExportAsync, () => !IsExporting);
            BrowseDestinationCommand = new AsyncRelayCommand(BrowseDestinationAsync);
            GoBackCommand = new RelayCommand(GoBack);
        }

        public async Task BrowseDestinationAsync()
        {
            if (_dialogService == null) return;

            var folder = await _dialogService.OpenFolderDialogAsync("Select Destination Folder");
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
            {
                string safeName = string.Join("_", _instance.Name.Split(Path.GetInvalidFileNameChars()));
                DestinationZipPath = Path.Combine(folder, $"{safeName}-export.zip");
            }
        }

        public async Task ExportAsync()
        {
            if (string.IsNullOrWhiteSpace(DestinationZipPath) || IsExporting) return;

            IsExporting = true;
            HasError = false;
            ErrorMessage = null;
            IsComplete = false;
            CurrentStep = "Packaging instance archive...";
            OverallProgress = 30;

            try
            {
                string? destDir = Path.GetDirectoryName(DestinationZipPath);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                OverallProgress = 60;
                await _instanceService.ExportInstanceAsync(_instance.Slug, DestinationZipPath);

                OverallProgress = 100;
                CurrentStep = $"Successfully exported to {Path.GetFileName(DestinationZipPath)}";
                IsComplete = true;

                _dialogService?.ShowMessage("Export Complete", $"Instance successfully exported to:\n{DestinationZipPath}", DialogType.Information);
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = ex.Message;
                CurrentStep = "Export failed.";
                _dialogService?.ShowMessage("Export Failed", ex.Message, DialogType.Error);
            }
            finally
            {
                IsExporting = false;
            }
        }

        private void GoBack()
        {
            var mainVM = App.Services?.GetService(typeof(MainWindowViewModel)) as MainWindowViewModel;
            if (mainVM != null)
            {
                var dashVM = App.Services?.GetService(typeof(DashboardViewModel)) as DashboardViewModel;
                if (dashVM != null)
                {
                    mainVM.CurrentViewModel = dashVM;
                }
            }
        }
    }
}
