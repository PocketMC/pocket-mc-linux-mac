using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PocketMC.Core.Models;
using PocketMC.Core.Services;

namespace PocketMC.App.ViewModels.Instances
{
    public partial class InstanceImportViewModel : ObservableObject
    {
        private readonly IInstanceService _instanceService;
        private readonly IDialogService? _dialogService;

        [ObservableProperty] private string _zipPath = string.Empty;
        [ObservableProperty] private string _requestedName = string.Empty;
        [ObservableProperty] private string _currentStep = "Choose an export ZIP package to import.";
        [ObservableProperty] private double _overallProgress;
        [ObservableProperty] private bool _isImporting;
        [ObservableProperty] private bool _isComplete;
        [ObservableProperty] private bool _hasError;
        [ObservableProperty] private string? _errorMessage;
        [ObservableProperty] private string? _detectedEngineType;
        [ObservableProperty] private string? _detectedVersion;

        public bool CanImport => !string.IsNullOrWhiteSpace(ZipPath) && File.Exists(ZipPath) && !string.IsNullOrWhiteSpace(RequestedName) && !IsImporting;

        public IAsyncRelayCommand ImportCommand { get; }
        public IAsyncRelayCommand BrowseZipCommand { get; }
        public IRelayCommand GoBackCommand { get; }

        public InstanceImportViewModel(
            IInstanceService instanceService,
            IDialogService? dialogService = null)
        {
            _instanceService = instanceService;
            _dialogService = dialogService;

            ImportCommand = new AsyncRelayCommand(ImportAsync, () => CanImport);
            BrowseZipCommand = new AsyncRelayCommand(BrowseZipAsync);
            GoBackCommand = new RelayCommand(GoBack);
        }

        public async Task BrowseZipAsync()
        {
            if (_dialogService == null) return;

            var file = await _dialogService.OpenFileDialogAsync("Select PocketMC Export Package", "ZIP Archives (*.zip)|*.zip|All Files (*.*)|*.*");
            if (!string.IsNullOrEmpty(file) && File.Exists(file))
            {
                ZipPath = file;
                InspectZipPackage(file);
            }
        }

        private void InspectZipPackage(string path)
        {
            try
            {
                using var archive = ZipFile.OpenRead(path);
                var manifestEntry = archive.GetEntry(InstanceExportManifest.ManifestFileName);
                if (manifestEntry != null)
                {
                    using var reader = new StreamReader(manifestEntry.Open());
                    var json = reader.ReadToEnd();
                    var manifest = JsonSerializer.Deserialize<InstanceExportManifest>(json);
                    if (manifest != null)
                    {
                        if (string.IsNullOrWhiteSpace(RequestedName))
                        {
                            RequestedName = manifest.ServerMeta.Name;
                        }
                        DetectedEngineType = manifest.Software.Type;
                        DetectedVersion = manifest.Software.MinecraftVersion;
                        CurrentStep = $"Detected: {DetectedEngineType} ({DetectedVersion})";
                        return;
                    }
                }

                // Fallback to filename as requested name
                if (string.IsNullOrWhiteSpace(RequestedName))
                {
                    RequestedName = Path.GetFileNameWithoutExtension(path).Replace("-export", "");
                }
                CurrentStep = "Ready to import.";
            }
            catch (Exception ex)
            {
                CurrentStep = $"Warning: Could not read manifest: {ex.Message}";
            }
        }

        public async Task ImportAsync()
        {
            if (!CanImport) return;

            IsImporting = true;
            HasError = false;
            ErrorMessage = null;
            IsComplete = false;
            CurrentStep = "Extracting and importing instance...";
            OverallProgress = 20;

            try
            {
                OverallProgress = 50;
                var instance = await _instanceService.ImportInstanceAsync(ZipPath, RequestedName);

                OverallProgress = 100;
                CurrentStep = $"Successfully imported '{instance.Name}'!";
                IsComplete = true;

                _dialogService?.ShowMessage("Import Complete", $"Instance '{instance.Name}' imported successfully.", DialogType.Information);
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = ex.Message;
                CurrentStep = "Import failed.";
                _dialogService?.ShowMessage("Import Failed", ex.Message, DialogType.Error);
            }
            finally
            {
                IsImporting = false;
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

        partial void OnZipPathChanged(string value)
        {
            ImportCommand.NotifyCanExecuteChanged();
        }

        partial void OnRequestedNameChanged(string value)
        {
            ImportCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsImportingChanged(bool value)
        {
            ImportCommand.NotifyCanExecuteChanged();
        }
    }
}
