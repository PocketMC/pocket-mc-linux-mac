using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PocketMC.Core.Services;

namespace PocketMC.App.ViewModels.Settings
{
    public partial class SettingsPerformanceVM : ObservableObject
    {
        private readonly IDialogService? _dialogService;
        private readonly Action _markDirty;
        private readonly double _totalRamMb;

        [ObservableProperty]
        private double _minRam = 1024;

        [ObservableProperty]
        private double _maxRam = 4096;

        [ObservableProperty]
        private bool _showRamWarning;

        [ObservableProperty]
        private string? _javaPath;

        [ObservableProperty]
        private string? _advancedJvmArgs;

        public string MinRamDisplay => $"{MinRam:N0} MB";
        public string MaxRamDisplay => $"{MaxRam:N0} MB";
        public double TotalRamMb => _totalRamMb;

        public IAsyncRelayCommand BrowseJavaCommand { get; }

        public SettingsPerformanceVM(Action markDirty, IDialogService? dialogService = null)
        {
            _markDirty = markDirty;
            _dialogService = dialogService;

            double memMb = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024.0 * 1024.0);
            _totalRamMb = memMb > 0 ? memMb : 8192;

            BrowseJavaCommand = new AsyncRelayCommand(BrowseJavaAsync);
        }

        public async Task BrowseJavaAsync()
        {
            if (_dialogService == null) return;

            string filter = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "java.exe|java.exe|Executables (*.exe)|*.exe"
                : "Java Executable (java)|java|All Files (*.*)|*.*";

            var file = await _dialogService.OpenFileDialogAsync("Select Java Executable", filter);
            if (!string.IsNullOrEmpty(file) && File.Exists(file))
            {
                JavaPath = file;
            }
        }

        private void CheckRamWarning()
        {
            if (_totalRamMb > 0)
            {
                ShowRamWarning = MaxRam > (_totalRamMb * 0.85);
            }
        }

        partial void OnMinRamChanged(double value)
        {
            OnPropertyChanged(nameof(MinRamDisplay));
            _markDirty();
        }

        partial void OnMaxRamChanged(double value)
        {
            OnPropertyChanged(nameof(MaxRamDisplay));
            CheckRamWarning();
            _markDirty();
        }

        partial void OnJavaPathChanged(string? value) => _markDirty();
        partial void OnAdvancedJvmArgsChanged(string? value) => _markDirty();
    }
}
