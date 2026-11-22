using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using PocketMC.Core.Models;
using PocketMC.Core.Services;

namespace PocketMC.App.ViewModels
{
    public partial class DashboardActionsViewModel : ObservableObject
    {
        private readonly IProcessRunner _processRunner;
        private readonly IInstanceService _instanceService;
        private readonly IDialogService? _dialogService;
        private readonly Action<InstanceCardViewModel>? _onOpenSettings;
        private readonly Action<InstanceCardViewModel>? _onOpenConsole;
        private readonly Action<InstanceCardViewModel>? _onOpenPlayers;
        private readonly Action<InstanceCardViewModel>? _onOpenExport;

        public DashboardActionsViewModel(
            IProcessRunner processRunner,
            IInstanceService instanceService,
            IDialogService? dialogService = null,
            Action<InstanceCardViewModel>? onOpenSettings = null,
            Action<InstanceCardViewModel>? onOpenConsole = null,
            Action<InstanceCardViewModel>? onOpenPlayers = null,
            Action<InstanceCardViewModel>? onOpenExport = null)
        {
            _processRunner = processRunner;
            _instanceService = instanceService;
            _dialogService = dialogService;
            _onOpenSettings = onOpenSettings;
            _onOpenConsole = onOpenConsole;
            _onOpenPlayers = onOpenPlayers;
            _onOpenExport = onOpenExport;
        }

        public async Task StartServerAsync(InstanceCardViewModel vm)
        {
            if (vm.IsRunning) return;

            try
            {
                vm.UpdateState("Starting");
                await _processRunner.StartAsync(vm.Instance);
                vm.UpdateState("Online", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                vm.UpdateState("Stopped");
                _dialogService?.ShowMessage("Failed to start server", ex.Message, DialogType.Error);
            }
        }

        public async Task StopServerAsync(InstanceCardViewModel vm)
        {
            if (!vm.IsRunning) return;

            try
            {
                vm.UpdateState("Stopping");
                await _processRunner.StopAsync(vm.Instance);
                vm.UpdateState("Stopped");
            }
            catch (Exception ex)
            {
                _dialogService?.ShowMessage("Failed to stop server", ex.Message, DialogType.Error);
            }
        }

        public async Task RestartServerAsync(InstanceCardViewModel vm)
        {
            vm.IsWaitingToRestart = true;
            try
            {
                await StopServerAsync(vm);
                await Task.Delay(1000);
                await StartServerAsync(vm);
            }
            finally
            {
                vm.IsWaitingToRestart = false;
            }
        }

        public async Task<bool> DeleteInstanceAsync(InstanceCardViewModel vm)
        {
            if (vm.IsRunning)
            {
                _dialogService?.ShowMessage("Cannot Delete", "Please stop the server before deleting it.", DialogType.Warning);
                return false;
            }

            if (_dialogService != null)
            {
                var result = await _dialogService.ShowDialogAsync(
                    "Delete Instance",
                    $"Are you sure you want to permanently delete instance '{vm.Name}'? This action cannot be undone.",
                    DialogType.Warning,
                    showCancel: true,
                    primaryButtonText: "Delete",
                    secondaryButtonText: "Cancel");

                if (result != DialogResult.Ok && result != DialogResult.Yes)
                {
                    return false;
                }
            }

            await _instanceService.DeleteInstanceAsync(vm.Instance.Slug);
            return true;
        }

        public async Task TogglePinAsync(InstanceCardViewModel vm)
        {
            vm.SetPinned(!vm.IsPinned);
            await _instanceService.SaveInstanceAsync(vm.Instance);
        }

        public void OpenFolder(InstanceCardViewModel vm)
        {
            string path = vm.Instance.Path;
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", $"\"{path}\"");
                }
                else
                {
                    Process.Start("xdg-open", $"\"{path}\"");
                }
            }
            catch
            {
                // Fallback or ignore OS launch error
            }
        }

        public async Task CopyCrashReportAsync(InstanceCardViewModel vm)
        {
            string crashDir = Path.Combine(vm.Instance.Path, "crash-reports");
            if (!Directory.Exists(crashDir))
            {
                _dialogService?.ShowMessage("No Crash Reports", "No crash reports were found for this instance.", DialogType.Information);
                return;
            }

            var latest = Directory.GetFiles(crashDir, "crash-*.txt")
                .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
                .FirstOrDefault();

            if (latest == null)
            {
                _dialogService?.ShowMessage("No Crash Reports", "No crash reports found.", DialogType.Information);
                return;
            }

            string content = await File.ReadAllTextAsync(latest);
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow?.Clipboard != null)
            {
                await desktop.MainWindow.Clipboard.SetTextAsync(content);
                _dialogService?.ShowMessage("Copied", "Latest crash report copied to clipboard.", DialogType.Information);
            }
        }

        public void OpenSettings(InstanceCardViewModel vm) => _onOpenSettings?.Invoke(vm);
        public void OpenConsole(InstanceCardViewModel vm) => _onOpenConsole?.Invoke(vm);
        public void OpenPlayers(InstanceCardViewModel vm) => _onOpenPlayers?.Invoke(vm);
        public void OpenExport(InstanceCardViewModel vm) => _onOpenExport?.Invoke(vm);
    }
}
