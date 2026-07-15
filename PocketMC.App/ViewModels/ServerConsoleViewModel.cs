using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PocketMC.Core.Models;
using PocketMC.Core.Services;

namespace PocketMC.App.ViewModels
{
    public class LogLine
    {
        public string Text { get; }
        public string ColorBrush { get; }

        public LogLine(string text)
        {
            Text = text;
            ColorBrush = Classify(text);
        }

        private string Classify(string text)
        {
            if (text.Contains("[WARN]") || text.Contains("WARN") || text.Contains("Warning"))
                return "#FFEB3B"; // Yellow
            if (text.Contains("[ERROR]") || text.Contains("ERROR") || text.Contains("Exception") || text.Contains("Failed"))
                return "#F44336"; // Red
            if (text.Contains("[System]") || text.Contains("System"))
                return "#9C27B0"; // Purple
            if (text.Contains("joined the game") || text.Contains("left the game") || (text.Contains("<") && text.Contains(">")))
                return "#8BC34A"; // Green (Chat / Player events)
            return "#E5E5E5"; // Light Gray / default
        }
    }

    public partial class ServerConsoleViewModel : ObservableObject, IDisposable
    {
        private readonly IInstanceService _instanceService;
        private readonly IConsoleLogService _consoleLogService;
        private readonly IProcessRunner _processRunner;

        private readonly List<string> _commandHistory = new();
        private int _historyIndex = -1;

        public ObservableCollection<ServerInstance> Instances { get; } = new();
        public ObservableCollection<LogLine> LogLines { get; } = new();

        [ObservableProperty]
        private ServerInstance? _selectedInstance;

        [ObservableProperty]
        private string _commandInput = string.Empty;

        [ObservableProperty]
        private bool _autoScroll = true;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private int _logEntryCount;

        [ObservableProperty]
        private bool _isCrashed;

        public IAsyncRelayCommand SendCommandCommand { get; }
        public IRelayCommand RefreshInstancesCommand { get; }
        public IRelayCommand GoBackCommand { get; }

        public ServerConsoleViewModel(
            IInstanceService instanceService,
            IConsoleLogService consoleLogService,
            IProcessRunner processRunner)
        {
            _instanceService = instanceService;
            _consoleLogService = consoleLogService;
            _processRunner = processRunner;

            SendCommandCommand = new AsyncRelayCommand(SendCommandAsync);
            RefreshInstancesCommand = new RelayCommand(RefreshInstances);
            GoBackCommand = new RelayCommand(GoBack);

            _consoleLogService.LogReceived += OnLogReceived;
            _consoleLogService.LogsCleared += OnLogsCleared;
            _processRunner.StateChanged += OnProcessStateChanged;

            RefreshInstances();
        }

        public void Initialize(ServerInstance instance)
        {
            _pendingSelectSlug = instance.Slug;
            var found = Instances.FirstOrDefault(i => i.Slug == instance.Slug);
            if (found != null)
            {
                SelectedInstance = found;
                _pendingSelectSlug = null;
            }
            else
            {
                SelectedInstance = instance;
            }
            UpdateCrashedState();
        }

        private string? _pendingSelectSlug;

        public void RefreshInstances()
        {
            _ = RefreshInstancesAsync();
        }

        public async Task RefreshInstancesAsync()
        {
            var list = await _instanceService.ListInstancesAsync();
            Dispatcher.UIThread.Post(() =>
            {
                var targetSlug = _pendingSelectSlug ?? SelectedInstance?.Slug;

                Instances.Clear();
                foreach (var inst in list)
                {
                    Instances.Add(inst);
                }

                if (targetSlug != null)
                {
                    var match = Instances.FirstOrDefault(i => i.Slug == targetSlug);
                    if (match != null)
                    {
                        SelectedInstance = match;
                        _pendingSelectSlug = null;
                        return;
                    }
                }

                if (SelectedInstance == null && Instances.Any())
                {
                    SelectedInstance = Instances.First();
                }
            });
        }

        [RelayCommand]
        public async Task CopyLogsAsync()
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                var clipboard = desktop.MainWindow?.Clipboard;
                if (clipboard != null)
                {
                    var fullText = string.Join(Environment.NewLine, LogLines.Select(l => l.Text));
                    await clipboard.SetTextAsync(fullText);
                }
            }
        }

        partial void OnSelectedInstanceChanged(ServerInstance? value)
        {
            ReloadLogs();
            UpdateCrashedState();
        }

        partial void OnSearchQueryChanged(string value)
        {
            ReloadLogs();
        }

        private void ReloadLogs()
        {
            LogLines.Clear();
            if (SelectedInstance == null)
            {
                LogEntryCount = 0;
                return;
            }

            IReadOnlyList<string> logs;
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                logs = _consoleLogService.GetLogs(SelectedInstance.Slug);
            }
            else
            {
                logs = _consoleLogService.SearchLogs(SelectedInstance.Slug, SearchQuery);
            }

            foreach (var log in logs)
            {
                LogLines.Add(new LogLine(log));
            }
            LogEntryCount = LogLines.Count;
        }

        private void OnLogReceived(string slug, string line)
        {
            if (SelectedInstance != null && SelectedInstance.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(SearchQuery) || line.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        LogLines.Add(new LogLine(line));
                        LogEntryCount = LogLines.Count;
                        UpdateCrashedState();
                    });
                }
            }
        }

        private async Task SendCommandAsync()
        {
            if (string.IsNullOrWhiteSpace(CommandInput) || SelectedInstance == null) return;

            var cmd = CommandInput.Trim();
            CommandInput = string.Empty;

            _commandHistory.Add(cmd);
            _historyIndex = _commandHistory.Count;

            _consoleLogService.WriteLog(SelectedInstance.Slug, $"[Console Input] > {cmd}");

            try
            {
                bool isRunning = _processRunner.TryGetRunningInfo(SelectedInstance.Slug, out _, out string state);
                if (isRunning && state == "Running")
                {
                    await _processRunner.SendCommandAsync(SelectedInstance, cmd);
                }
                else
                {
                    _consoleLogService.WriteLog(SelectedInstance.Slug, "[System] Cannot send command: Server instance is not running.");
                }
            }
            catch (Exception ex)
            {
                _consoleLogService.WriteLog(SelectedInstance.Slug, $"[Console Error] Could not send command: {ex.Message}");
            }
        }

        public string CycleHistory(bool up)
        {
            if (!_commandHistory.Any()) return CommandInput;

            if (up)
            {
                if (_historyIndex > 0)
                {
                    _historyIndex--;
                }
                else if (_historyIndex == -1)
                {
                    _historyIndex = _commandHistory.Count - 1;
                }
            }
            else
            {
                if (_historyIndex >= 0 && _historyIndex < _commandHistory.Count - 1)
                {
                    _historyIndex++;
                }
                else
                {
                    _historyIndex = _commandHistory.Count;
                    CommandInput = string.Empty;
                    return string.Empty;
                }
            }

            if (_historyIndex >= 0 && _historyIndex < _commandHistory.Count)
            {
                CommandInput = _commandHistory[_historyIndex];
            }

            return CommandInput;
        }

        private void UpdateCrashedState()
        {
            if (SelectedInstance == null)
            {
                IsCrashed = false;
                return;
            }

            _processRunner.TryGetRunningInfo(SelectedInstance.Slug, out _, out string state);
            IsCrashed = (state == "Crashed");
        }

        [RelayCommand]
        private void NavigateToAiDiagnostic()
        {
            if (SelectedInstance == null) return;
            var mainVM = App.Services.GetService(typeof(MainWindowViewModel)) as MainWindowViewModel;
            if (mainVM != null)
            {
                var diagnosticVM = App.Services.GetRequiredService<AiDiagnosticViewModel>();
                diagnosticVM.Initialize(SelectedInstance);
                mainVM.CurrentViewModel = diagnosticVM;
            }
        }

        private void GoBack()
        {
            var mainVM = App.Services.GetService(typeof(MainWindowViewModel)) as MainWindowViewModel;
            if (mainVM != null)
            {
                var dashVM = App.Services.GetService(typeof(DashboardViewModel)) as DashboardViewModel;
                if (dashVM != null)
                {
                    mainVM.CurrentViewModel = dashVM;
                }
            }
        }

        private void OnLogsCleared(string slug)
        {
            if (SelectedInstance != null && SelectedInstance.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase))
            {
                Dispatcher.UIThread.Post(() =>
                {
                    LogLines.Clear();
                    LogEntryCount = 0;
                });
            }
        }

        private void OnProcessStateChanged(string slug, string state)
        {
            if (SelectedInstance != null && SelectedInstance.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase))
            {
                Dispatcher.UIThread.Post(() =>
                {
                    IsCrashed = (state == "Crashed");
                });
            }
        }

        public void Dispose()
        {
            _consoleLogService.LogReceived -= OnLogReceived;
            _consoleLogService.LogsCleared -= OnLogsCleared;
            _processRunner.StateChanged -= OnProcessStateChanged;
            GC.SuppressFinalize(this);
        }
    }
}
