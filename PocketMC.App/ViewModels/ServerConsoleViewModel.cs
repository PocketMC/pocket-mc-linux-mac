using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.App.Services.Console;
using PocketMC.Infrastructure.Services;

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
        private readonly ISettingsService? _settingsService;
        private readonly IServerLifecycleService? _lifecycleService;
        private readonly LlmProviderFactory? _llmProviderFactory;
        private readonly SessionSummarizationService? _summarizationService;
        private readonly ProcessMetricsTracker? _metricsTracker;
        private readonly IPlayerService? _playerService;

        private readonly List<string> _commandHistory = new();
        private int _historyIndex = -1;
        private bool _crashBannerDismissed;
        private int _onlinePlayerCount;
        private DispatcherTimer? _metricsTimer;

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

        [ObservableProperty]
        private string _copyButtonText = "Copy Logs";

        [ObservableProperty]
        private bool _isCopied;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsCrashBannerVisible))]
        private string _crashLogText = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsReadOnlySessionLog))]
        [NotifyPropertyChangedFor(nameof(StatusText))]
        [NotifyPropertyChangedFor(nameof(StatusDotBrush))]
        private bool _isServerOnline;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsReadOnlySessionLog))]
        [NotifyPropertyChangedFor(nameof(StatusText))]
        [NotifyPropertyChangedFor(nameof(StatusDotBrush))]
        private string _serverState = "Stopped";

        [ObservableProperty]
        private string _playerStatusText = "0 / 20";

        [ObservableProperty]
        private bool _isResourceWarningVisible;

        [ObservableProperty]
        private string _resourceWarningTitle = "High Resource Usage";

        [ObservableProperty]
        private string _resourceWarningMessage = "Your server is using over 80% of its assigned RAM. Performance may be impacted.";

        [ObservableProperty]
        private bool _isResourceWarningSuccess;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AiPanelWidth))]
        private bool _isAiPanelOpen;

        [ObservableProperty]
        private bool _isAiLoading;

        [ObservableProperty]
        private string _aiStatusText = string.Empty;

        [ObservableProperty]
        private string _aiMarkdown = string.Empty;

        public GridLength AiPanelWidth => IsAiPanelOpen ? new GridLength(390) : new GridLength(0);
        public bool IsReadOnlySessionLog => !IsServerOnline && !string.Equals(ServerState, "Starting", StringComparison.OrdinalIgnoreCase);
        public bool IsCrashBannerVisible => IsCrashed && !_crashBannerDismissed;

        public string StatusText => ServerState switch
        {
            "Running" or "Online" => "Online",
            "Starting" => "Starting...",
            "Stopping" => "Stopping...",
            "Crashed" => "Crashed",
            _ => "Stopped"
        };

        public IBrush StatusDotBrush => new SolidColorBrush(Color.Parse(ServerState switch
        {
            "Running" or "Online" => "#10b981",
            "Starting" or "Stopping" => "#f59e0b",
            "Crashed" => "#ef4444",
            _ => "#9ca3af"
        }));

        public IAsyncRelayCommand SendCommandCommand { get; }
        public IRelayCommand RefreshInstancesCommand { get; }
        public IRelayCommand GoBackCommand { get; }
        public IAsyncRelayCommand StopServerCommand { get; }
        public IAsyncRelayCommand RestartServerCommand { get; }
        public IRelayCommand NavigateToPlayersCommand { get; }
        public IRelayCommand ToggleAiSummaryCommand { get; }
        public IAsyncRelayCommand SummarizeNowCommand { get; }
        public IRelayCommand CloseAiPanelCommand { get; }
        public IAsyncRelayCommand<string?> AnalyzeSnippetCommand { get; }
        public IAsyncRelayCommand AnalyzeCrashCommand { get; }
        public IAsyncRelayCommand CopyCrashReportCommand { get; }
        public IRelayCommand DismissCrashBannerCommand { get; }
        public IAsyncRelayCommand OptimizeServerCommand { get; }

        public ServerConsoleViewModel(
            IInstanceService instanceService,
            IConsoleLogService consoleLogService,
            IProcessRunner processRunner,
            ISettingsService? settingsService = null,
            IServerLifecycleService? lifecycleService = null,
            LlmProviderFactory? llmProviderFactory = null,
            SessionSummarizationService? summarizationService = null,
            ProcessMetricsTracker? metricsTracker = null,
            IPlayerService? playerService = null)
        {
            _instanceService = instanceService;
            _consoleLogService = consoleLogService;
            _processRunner = processRunner;
            _settingsService = settingsService ?? (App.Services?.GetService(typeof(ISettingsService)) as ISettingsService);
            _lifecycleService = lifecycleService ?? (App.Services?.GetService(typeof(IServerLifecycleService)) as IServerLifecycleService);
            _llmProviderFactory = llmProviderFactory ?? (App.Services?.GetService(typeof(LlmProviderFactory)) as LlmProviderFactory);
            _summarizationService = summarizationService ?? (App.Services?.GetService(typeof(SessionSummarizationService)) as SessionSummarizationService);
            _metricsTracker = metricsTracker ?? (App.Services?.GetService(typeof(ProcessMetricsTracker)) as ProcessMetricsTracker);
            _playerService = playerService ?? (App.Services?.GetService(typeof(IPlayerService)) as IPlayerService);

            SendCommandCommand = new AsyncRelayCommand(SendCommandAsync);
            RefreshInstancesCommand = new RelayCommand(RefreshInstances);
            GoBackCommand = new RelayCommand(GoBack);
            StopServerCommand = new AsyncRelayCommand(StopServerAsync);
            RestartServerCommand = new AsyncRelayCommand(RestartServerAsync);
            NavigateToPlayersCommand = new RelayCommand(NavigateToPlayers);
            ToggleAiSummaryCommand = new RelayCommand(() => _ = ToggleAiSummaryAsync());
            SummarizeNowCommand = new AsyncRelayCommand(SummarizeSessionAsync);
            CloseAiPanelCommand = new RelayCommand(() => IsAiPanelOpen = false);
            AnalyzeSnippetCommand = new AsyncRelayCommand<string?>(AnalyzeSnippetAsync);
            AnalyzeCrashCommand = new AsyncRelayCommand(AnalyzeCrashInlineAsync);
            CopyCrashReportCommand = new AsyncRelayCommand(CopyCrashReportAsync);
            DismissCrashBannerCommand = new RelayCommand(DismissCrashBanner);
            OptimizeServerCommand = new AsyncRelayCommand(OptimizeServerAsync);

            _consoleLogService.LogReceived += OnLogReceived;
            _consoleLogService.LogsCleared += OnLogsCleared;
            _processRunner.StateChanged += OnProcessStateChanged;
            if (_summarizationService != null)
            {
                _summarizationService.SummarizationStarted += OnSummarizationStarted;
                _summarizationService.SummarizationCompleted += OnSummarizationCompleted;
            }

            StartMetricsMonitor();
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
            var textToCopy = LogLines.Count > 0
                ? string.Join(Environment.NewLine, LogLines.Select(l => l.Text))
                : string.Join(Environment.NewLine, _consoleLogService.GetLogs(SelectedInstance?.Slug ?? string.Empty));

            if (string.IsNullOrEmpty(textToCopy))
            {
                return;
            }

            bool success = await PocketMC.App.Services.AppClipboard.SetTextAsync(textToCopy);
            if (success)
            {
                CopyButtonText = "Copied!";
                IsCopied = true;
                _ = Task.Delay(2000).ContinueWith(_ =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        CopyButtonText = "Copy Logs";
                        IsCopied = false;
                    });
                });
            }
        }

        partial void OnSelectedInstanceChanged(ServerInstance? value)
        {
            _crashBannerDismissed = false;
            IsResourceWarningVisible = false;
            ReloadLogs();
            UpdateCrashedState();
            InitializeAiSummaryState();
            _ = RefreshPlayerStatusAsync();
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
            if (cmd.StartsWith('/'))
            {
                cmd = cmd.Substring(1).Trim();
                if (string.IsNullOrEmpty(cmd))
                {
                    CommandInput = string.Empty;
                    return;
                }
            }

            CommandInput = string.Empty;

            if (_commandHistory.Count == 0 || _commandHistory[^1] != cmd)
            {
                _commandHistory.Add(cmd);
            }
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
                ServerState = "Stopped";
                IsServerOnline = false;
                IsCrashed = false;
                CrashLogText = string.Empty;
                OnPropertyChanged(nameof(IsCrashBannerVisible));
                return;
            }

            bool hasInfo = _processRunner.TryGetRunningInfo(SelectedInstance.Slug, out _, out string state);
            ServerState = hasInfo ? state : "Stopped";
            IsServerOnline = hasInfo && (string.Equals(state, "Running", StringComparison.OrdinalIgnoreCase) || string.Equals(state, "Online", StringComparison.OrdinalIgnoreCase));
            IsCrashed = string.Equals(state, "Crashed", StringComparison.OrdinalIgnoreCase);

            if (IsCrashed)
            {
                var errorTail = _consoleLogService.GetLogs(SelectedInstance.Slug).TakeLast(25).ToList();
                CrashLogText = errorTail.Count > 0
                    ? string.Join(Environment.NewLine, errorTail)
                    : "The server process exited unexpectedly. Inspect console logs above or use AI Diagnostics.";
            }
            else if (string.Equals(state, "Starting", StringComparison.OrdinalIgnoreCase) || IsServerOnline)
            {
                _crashBannerDismissed = false;
                CrashLogText = string.Empty;
            }

            OnPropertyChanged(nameof(IsCrashBannerVisible));
        }

        private void StartMetricsMonitor()
        {
            if (_metricsTracker == null)
            {
                return;
            }

            try
            {
                _metricsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                _metricsTimer.Tick += (_, _) => UpdateResourceWarning();
                _metricsTimer.Start();
            }
            catch
            {
                // Headless test runs without active dispatcher platform can safely skip periodic polling.
            }
        }

        private void UpdateResourceWarning()
        {
            if (SelectedInstance == null || _metricsTracker == null ||
                !_processRunner.TryGetRunningInfo(SelectedInstance.Slug, out int pgid, out string state) ||
                !string.Equals(state, "Running", StringComparison.OrdinalIgnoreCase) ||
                pgid <= 0)
            {
                if (!IsResourceWarningSuccess)
                {
                    IsResourceWarningVisible = false;
                }
                return;
            }

            try
            {
                var metrics = _metricsTracker.GetGroupMetrics(pgid);
                double ramMb = metrics.MemoryBytes / (1024.0 * 1024.0);
                int maxRam = SelectedInstance.MaxRamMb;
                if (maxRam > 0 && ramMb > maxRam * 0.8 && !IsResourceWarningSuccess)
                {
                    ResourceWarningTitle = "High Resource Usage";
                    ResourceWarningMessage = "Your server is using over 80% of its assigned RAM. Performance may be impacted.";
                    IsResourceWarningVisible = true;
                }
                else if (!IsResourceWarningSuccess)
                {
                    IsResourceWarningVisible = false;
                }
            }
            catch
            {
            }
        }

        private async Task OptimizeServerAsync()
        {
            if (SelectedInstance == null || !IsServerOnline)
            {
                return;
            }

            try
            {
                if (_processRunner.TryGetRunningInfo(SelectedInstance.Slug, out int pid, out _) && pid > 0)
                {
                    try
                    {
                        using var proc = Process.GetProcessById(pid);
                        if (!proc.HasExited)
                        {
                            proc.PriorityClass = ProcessPriorityClass.High;
                        }
                    }
                    catch
                    {
                    }
                }

                await _processRunner.SendCommandAsync(SelectedInstance, "gc");
                await _processRunner.SendCommandAsync(SelectedInstance, "spark gc");

                IsResourceWarningSuccess = true;
                ResourceWarningTitle = "Optimization Applied";
                ResourceWarningMessage = "Process priority elevated and GC sweep requested.";
                IsResourceWarningVisible = true;

                await Task.Delay(3000);
                IsResourceWarningSuccess = false;
                IsResourceWarningVisible = false;
                ResourceWarningTitle = "High Resource Usage";
                ResourceWarningMessage = "Your server is using over 80% of its assigned RAM. Performance may be impacted.";
            }
            catch (Exception ex)
            {
                _consoleLogService.WriteLog(SelectedInstance.Slug, $"[System] Resource optimization failed: {ex.Message}");
            }
        }

        private async Task StopServerAsync()
        {
            if (SelectedInstance == null || !IsServerOnline)
            {
                return;
            }

            try
            {
                if (_lifecycleService != null)
                {
                    await _lifecycleService.StopAsync(SelectedInstance);
                }
                else
                {
                    await _processRunner.StopAsync(SelectedInstance);
                }
            }
            catch (Exception ex)
            {
                _consoleLogService.WriteLog(SelectedInstance.Slug, $"[ERROR] Stop failed: {ex.Message}");
            }
        }

        private async Task RestartServerAsync()
        {
            if (SelectedInstance == null)
            {
                return;
            }

            try
            {
                _consoleLogService.WriteLog(SelectedInstance.Slug, "[PocketMC] Initiating manual restart...");
                if (_lifecycleService != null)
                {
                    await _lifecycleService.RestartAsync(SelectedInstance);
                }
                else
                {
                    await _processRunner.StopAsync(SelectedInstance);
                    await _processRunner.StartAsync(SelectedInstance);
                }
            }
            catch (Exception ex)
            {
                _consoleLogService.WriteLog(SelectedInstance.Slug, $"[ERROR] Restart failed: {ex.Message}");
            }
        }

        private void NavigateToPlayers()
        {
            if (SelectedInstance == null)
            {
                return;
            }

            var navService = App.Services?.GetService(typeof(PocketMC.App.Navigation.IAppNavigationService)) as PocketMC.App.Navigation.IAppNavigationService;
            var playersVm = App.Services?.GetService(typeof(PlayerManagementViewModel)) as PlayerManagementViewModel;
            if (playersVm != null)
            {
                playersVm.Initialize(SelectedInstance);
                if (navService != null)
                {
                    navService.NavigateToDetailPage(
                        playersVm,
                        $"Players: {SelectedInstance.Name}",
                        PocketMC.App.Navigation.DetailRouteKind.PlayerManagement,
                        PocketMC.App.Navigation.DetailBackNavigation.PreviousDetail);
                }
            }
        }

        private void DismissCrashBanner()
        {
            _crashBannerDismissed = true;
            OnPropertyChanged(nameof(IsCrashBannerVisible));
        }

        private async Task CopyCrashReportAsync()
        {
            if (string.IsNullOrWhiteSpace(CrashLogText))
            {
                return;
            }

            await PocketMC.App.Services.AppClipboard.SetTextAsync(CrashLogText);
        }

        private async Task AnalyzeCrashInlineAsync()
        {
            string context = !string.IsNullOrWhiteSpace(CrashLogText)
                ? CrashLogText
                : string.Join(Environment.NewLine, LogLines.TakeLast(40).Select(l => l.Text));
            await AnalyzeSnippetAsync(context);
        }

        private void InitializeAiSummaryState()
        {
            if (SelectedInstance == null || _summarizationService == null || string.IsNullOrWhiteSpace(SelectedInstance.Path))
            {
                AiMarkdown = string.Empty;
                IsAiLoading = false;
                AiStatusText = string.Empty;
                return;
            }

            if (_summarizationService.IsSummarizing(SelectedInstance.Path))
            {
                IsAiPanelOpen = true;
                AiStatusText = "Generating session summary...";
                IsAiLoading = true;
                AiMarkdown = string.Empty;
            }
            else
            {
                SessionSummary? latest = _summarizationService.GetLatestSummary(SelectedInstance.Path);
                AiMarkdown = latest?.Content ?? string.Empty;
                IsAiLoading = false;
                AiStatusText = string.Empty;
            }
        }

        private void OnSummarizationStarted(string serverDir)
        {
            if (SelectedInstance == null || !string.Equals(serverDir, SelectedInstance.Path, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            PostToUi(() =>
            {
                IsAiPanelOpen = true;
                AiStatusText = "Generating session summary...";
                IsAiLoading = true;
                AiMarkdown = string.Empty;
            });
        }

        private void OnSummarizationCompleted(string serverDir, SummarizationResult result)
        {
            if (SelectedInstance == null || !string.Equals(serverDir, SelectedInstance.Path, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            PostToUi(() =>
            {
                IsAiLoading = false;
                AiStatusText = string.Empty;
                if (result.Success && result.Summary != null)
                {
                    AiMarkdown = result.Summary.Content;
                    IsAiPanelOpen = true;
                }
                else
                {
                    AiMarkdown = $"Summarization failure: {result.Error}";
                }
            });
        }

        private async Task ToggleAiSummaryAsync()
        {
            if (SelectedInstance != null && _summarizationService != null && !string.IsNullOrWhiteSpace(SelectedInstance.Path))
            {
                if (_summarizationService.IsSummarizing(SelectedInstance.Path))
                {
                    IsAiPanelOpen = true;
                    return;
                }

                if (!IsAiPanelOpen && !string.IsNullOrWhiteSpace(AiMarkdown))
                {
                    IsAiPanelOpen = true;
                    return;
                }
            }

            await SummarizeSessionAsync();
        }

        private async Task SummarizeSessionAsync()
        {
            if (SelectedInstance == null)
            {
                return;
            }

            if (_summarizationService == null || _settingsService == null)
            {
                NavigateToAiDiagnostic();
                return;
            }

            IsAiPanelOpen = true;
            AiStatusText = "Generating session summary...";
            IsAiLoading = true;
            AiMarkdown = string.Empty;

            try
            {
                PocketMC.Core.Models.Settings settings = _settingsService.Settings;
                if (!settings.IsAiConfigured())
                {
                    AiMarkdown = "AI is not configured in App Settings. Configure an API Key or select Ollama to generate summaries.";
                    return;
                }

                DateTime start = _lifecycleService?.GetSessionStartTime(SelectedInstance.Slug) ?? DateTime.UtcNow.AddMinutes(-30);
                SummarizationResult result = await _summarizationService.SummarizeAsync(
                    SelectedInstance.Path,
                    SelectedInstance.Name,
                    settings,
                    start,
                    DateTime.UtcNow);

                AiMarkdown = result.Success && result.Summary != null
                    ? result.Summary.Content
                    : $"Summarization failure: {result.Error}";
            }
            catch (Exception ex)
            {
                AiMarkdown = $"Unexpected error: {ex.Message}";
            }
            finally
            {
                IsAiLoading = false;
                AiStatusText = string.Empty;
            }
        }

        private async Task AnalyzeSnippetAsync(string? contextText)
        {
            string snippet = (contextText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(snippet))
            {
                return;
            }

            IsAiPanelOpen = true;
            AiStatusText = "Analyzing text...";
            IsAiLoading = true;
            AiMarkdown = string.Empty;

            try
            {
                if (_settingsService == null || _llmProviderFactory == null || !_settingsService.Settings.IsAiConfigured())
                {
                    AiMarkdown = "Error: AI not configured in App Settings. Configure an API Key or select Ollama.";
                    return;
                }

                const string prompt =
                    "You are a Minecraft Server Expert. Analyze the following log snippet and explain:\n" +
                    "1. What the error or message means in simple terms.\n" +
                    "2. The most likely root cause.\n" +
                    "3. Specific steps to fix it.\n\n" +
                    "Keep the response concise and formatted in Markdown.";

                DiagnosticResult result = await _llmProviderFactory.SendConfiguredRequestAsync(
                    _settingsService.Settings,
                    prompt,
                    snippet);

                AiMarkdown = result.Success
                    ? SummaryEmojiFormatter.Apply(result.Content)
                    : $"Analysis failure: {result.ErrorMessage}";
            }
            catch (Exception ex)
            {
                AiMarkdown = $"Unexpected error: {ex.Message}";
            }
            finally
            {
                IsAiLoading = false;
                AiStatusText = string.Empty;
            }
        }

        private async Task RefreshPlayerStatusAsync()
        {
            if (SelectedInstance == null)
            {
                PlayerStatusText = "0 / 20";
                return;
            }

            int count = _onlinePlayerCount;
            if (_playerService != null)
            {
                try
                {
                    var online = await _playerService.GetOnlinePlayersAsync(SelectedInstance);
                    count = online.Count;
                    _onlinePlayerCount = count;
                }
                catch
                {
                }
            }

            PostToUi(() => PlayerStatusText = $"{count} / {SelectedInstance.MaxPlayers}");
        }

        private static void PostToUi(Action action)
        {
            try
            {
                if (Dispatcher.UIThread.CheckAccess())
                {
                    action();
                }
                else
                {
                    Dispatcher.UIThread.Post(action);
                }
            }
            catch
            {
                action();
            }
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
                PostToUi(() =>
                {
                    UpdateCrashedState();
                    _ = RefreshPlayerStatusAsync();
                });
            }
        }

        public void Dispose()
        {
            _metricsTimer?.Stop();
            _consoleLogService.LogReceived -= OnLogReceived;
            _consoleLogService.LogsCleared -= OnLogsCleared;
            _processRunner.StateChanged -= OnProcessStateChanged;
            if (_summarizationService != null)
            {
                _summarizationService.SummarizationStarted -= OnSummarizationStarted;
                _summarizationService.SummarizationCompleted -= OnSummarizationCompleted;
            }
            GC.SuppressFinalize(this);
        }
    }
}
