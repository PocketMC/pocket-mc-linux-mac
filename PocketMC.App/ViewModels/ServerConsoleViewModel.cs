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
using PocketMC.App.Services.Console;

namespace PocketMC.App.ViewModels
{
    public class LogLine
    {
        public string Text { get; }
        public string ColorBrush { get; }
        public ConsoleLogLevel Level { get; }

        public LogLine(string text, ConsoleLogLevel previousLevel = ConsoleLogLevel.Info)
        {
            Text = text;
            Level = LogLineClassifier.Classify(text, previousLevel);
            ColorBrush = LogLineClassifier.GetHexColor(Level);
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
        private bool _isRegexEnabled;

        [ObservableProperty]
        private bool _isFilterChat = true;

        [ObservableProperty]
        private bool _isFilterInfo = true;

        [ObservableProperty]
        private bool _isFilterWarn = true;

        [ObservableProperty]
        private bool _isFilterError = true;

        [ObservableProperty]
        private bool _isFilterSystem = true;

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

        partial void OnSearchQueryChanged(string value) => ReloadLogs();
        partial void OnIsRegexEnabledChanged(bool value) => ReloadLogs();
        partial void OnIsFilterChatChanged(bool value) => ReloadLogs();
        partial void OnIsFilterInfoChanged(bool value) => ReloadLogs();
        partial void OnIsFilterWarnChanged(bool value) => ReloadLogs();
        partial void OnIsFilterErrorChanged(bool value) => ReloadLogs();
        partial void OnIsFilterSystemChanged(bool value) => ReloadLogs();

        private bool MatchesFilter(ConsoleLogLevel level) => level switch
        {
            ConsoleLogLevel.Chat => IsFilterChat,
            ConsoleLogLevel.Info => IsFilterInfo,
            ConsoleLogLevel.Warn => IsFilterWarn,
            ConsoleLogLevel.Error => IsFilterError,
            ConsoleLogLevel.System => IsFilterSystem,
            _ => true
        };

        private void ReloadLogs()
        {
            LogLines.Clear();
            if (SelectedInstance == null)
            {
                LogEntryCount = 0;
                return;
            }

            var allLogs = _consoleLogService.GetLogs(SelectedInstance.Slug);
            ConsoleLogLevel prevLevel = ConsoleLogLevel.Info;
            foreach (var log in allLogs)
            {
                if (string.IsNullOrWhiteSpace(SearchQuery) || ConsoleLogFilter.MatchesSearch(log, SearchQuery, IsRegexEnabled))
                {
                    var line = new LogLine(log, prevLevel);
                    prevLevel = line.Level;
                    if (MatchesFilter(line.Level))
                    {
                        LogLines.Add(line);
                    }
                }
            }
            LogEntryCount = LogLines.Count;
        }

        private void OnLogReceived(string slug, string line)
        {
            if (SelectedInstance != null && SelectedInstance.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(SearchQuery) || ConsoleLogFilter.MatchesSearch(line, SearchQuery, IsRegexEnabled))
                {
                    void AddLine()
                    {
                        var prevLevel = LogLines.Count > 0 ? LogLines[^1].Level : ConsoleLogLevel.Info;
                        var logLine = new LogLine(line, prevLevel);
                        if (MatchesFilter(logLine.Level))
                        {
                            LogLines.Add(logLine);
                            LogEntryCount = LogLines.Count;
                        }
                        UpdateCrashedState();
                    }

                    if (Dispatcher.UIThread.CheckAccess())
                    {
                        AddLine();
                    }
                    else
                    {
                        Dispatcher.UIThread.Post(AddLine);
                    }
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

        [RelayCommand]
        private void SendQuickCommand(string? cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd)) return;
            CommandInput = cmd;
            _ = SendCommandAsync();
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
            var navService = App.Services?.GetService(typeof(PocketMC.App.Navigation.IAppNavigationService)) as PocketMC.App.Navigation.IAppNavigationService;
            var diagnosticVM = App.Services?.GetRequiredService<AiDiagnosticViewModel>();
            if (diagnosticVM != null)
            {
                diagnosticVM.Initialize(SelectedInstance);
                if (navService != null)
                {
                    navService.NavigateToDetailPage(
                        diagnosticVM,
                        $"{SelectedInstance.Name} AI Diagnostics",
                        PocketMC.App.Navigation.DetailRouteKind.AiDiagnostic,
                        PocketMC.App.Navigation.DetailBackNavigation.PreviousDetail);
                }
                else
                {
                    var mainVM = App.Services?.GetService(typeof(MainWindowViewModel)) as MainWindowViewModel;
                    if (mainVM != null) mainVM.CurrentViewModel = diagnosticVM;
                }
            }
        }

        private void GoBack()
        {
            var navService = App.Services?.GetService(typeof(PocketMC.App.Navigation.IAppNavigationService)) as PocketMC.App.Navigation.IAppNavigationService;
            if (navService?.CanNavigateBack == true)
            {
                navService.NavigateBack();
            }
            else if (navService != null)
            {
                navService.NavigateToDashboard();
            }
            else
            {
                var mainVM = App.Services?.GetService(typeof(MainWindowViewModel)) as MainWindowViewModel;
                var dashVM = App.Services?.GetService(typeof(DashboardViewModel)) as DashboardViewModel;
                if (mainVM != null && dashVM != null)
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
