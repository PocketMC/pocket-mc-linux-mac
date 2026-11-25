using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PocketMC.App.ViewModels.Players;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;

namespace PocketMC.App.ViewModels
{
    public partial class PlayerManagementViewModel : ObservableObject, IDisposable
    {
        private readonly IInstanceService _instanceService;
        private readonly IPlayerService _playerService;
        private readonly IProcessRunner _processRunner;
        private readonly BanSidecarService _banSidecarService;
        private readonly IConsoleLogService? _consoleLogService;
        private readonly SemaphoreSlim _fileLock = new(1, 1);
        private readonly SemaphoreSlim _loadLock = new(1, 1);
        private readonly object _collectionLock = new();

        private FileSystemWatcher? _watcher;
        private DispatcherTimer? _timer;
        private DateTime? _lastUpdatedUtc;

        [ObservableProperty]
        private ServerInstance? _selectedInstance;

        [ObservableProperty] private string _newWhitelistPlayer = string.Empty;
        [ObservableProperty] private string _newBanPlayer = string.Empty;
        [ObservableProperty] private string _newOpPlayer = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsOnlineTabSelected))]
        [NotifyPropertyChangedFor(nameof(IsBanTabSelected))]
        [NotifyPropertyChangedFor(nameof(IsWhitelistTabSelected))]
        private int _selectedTabIndex;

        [ObservableProperty]
        private bool _isRefreshingState;

        [ObservableProperty]
        private string _lastUpdatedText = "Waiting for player list";

        [ObservableProperty]
        private bool _isWhitelistEnabled;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ServerStatusText))]
        [NotifyPropertyChangedFor(nameof(ServerStatusBrush))]
        [NotifyPropertyChangedFor(nameof(EmptyOnlineText))]
        private bool _isServerOnline;

        public bool IsOnlineTabSelected => SelectedTabIndex == 0;
        public bool IsBanTabSelected => SelectedTabIndex == 1;
        public bool IsWhitelistTabSelected => SelectedTabIndex == 2;

        public bool HasOnlinePlayers => OnlinePlayers.Count > 0;
        public bool HasBannedPlayers => BanListEntries.Count > 0;
        public bool HasWhitelistedPlayers => WhitelistEntries.Count > 0;

        public string EmptyOnlineText => IsServerOnline
            ? "No players currently online."
            : "Start the server to inspect and manage connected players.";

        public string EmptyBanText => "No banned players found.";
        public string EmptyWhitelistText => "No whitelisted players found.";

        public string ServerStatusText => IsServerOnline ? "Online" : "Offline";
        public IBrush ServerStatusBrush => new SolidColorBrush(Color.Parse(IsServerOnline ? "#10b981" : "#9ca3af"));

        public ObservableCollection<string> OnlinePlayers { get; } = new();
        public ObservableCollection<string> Whitelist { get; } = new();
        public ObservableCollection<string> Ops { get; } = new();
        public ObservableCollection<string> BannedPlayers { get; } = new();

        public ObservableCollection<PlayerViewModel> OnlinePlayerCards { get; } = new();
        public ObservableCollection<BannedPlayerViewModel> BanListEntries { get; } = new();
        public ObservableCollection<WhitelistPlayerViewModel> WhitelistEntries { get; } = new();

        public IAsyncRelayCommand<string> KickPlayerCommand { get; }
        public IAsyncRelayCommand<string> BanPlayerCommand { get; }
        public IAsyncRelayCommand<string> UnbanPlayerCommand { get; }
        public IAsyncRelayCommand AddWhitelistPlayerCommand { get; }
        public IAsyncRelayCommand<string> RemoveWhitelistPlayerCommand { get; }
        public IAsyncRelayCommand AddOpPlayerCommand { get; }
        public IAsyncRelayCommand<string> RemoveOpPlayerCommand { get; }
        public IAsyncRelayCommand ToggleWhitelistCommand { get; }
        public IAsyncRelayCommand ReloadWhitelistCommand { get; }
        public IAsyncRelayCommand RefreshPlayersCommand { get; }
        public IRelayCommand<string> SelectTabCommand { get; }
        public IRelayCommand GoBackCommand { get; }

        public PlayerManagementViewModel(
            IInstanceService instanceService,
            IPlayerService playerService,
            IProcessRunner processRunner,
            BanSidecarService? banSidecarService = null,
            IConsoleLogService? consoleLogService = null)
        {
            _instanceService = instanceService;
            _playerService = playerService;
            _processRunner = processRunner;
            _banSidecarService = banSidecarService ?? new BanSidecarService();
            _consoleLogService = consoleLogService ?? (App.Services?.GetService(typeof(IConsoleLogService)) as IConsoleLogService);

            KickPlayerCommand = new AsyncRelayCommand<string>(KickPlayerAsync);
            BanPlayerCommand = new AsyncRelayCommand<string>(BanPlayerAsync);
            UnbanPlayerCommand = new AsyncRelayCommand<string>(UnbanPlayerAsync);
            AddWhitelistPlayerCommand = new AsyncRelayCommand(AddWhitelistPlayerAsync);
            RemoveWhitelistPlayerCommand = new AsyncRelayCommand<string>(RemoveWhitelistPlayerAsync);
            AddOpPlayerCommand = new AsyncRelayCommand(AddOpPlayerAsync);
            RemoveOpPlayerCommand = new AsyncRelayCommand<string>(RemoveOpPlayerAsync);
            ToggleWhitelistCommand = new AsyncRelayCommand(ToggleWhitelistAsync);
            ReloadWhitelistCommand = new AsyncRelayCommand(ReloadWhitelistAsync);
            RefreshPlayersCommand = new AsyncRelayCommand(RefreshAllAsync);
            SelectTabCommand = new RelayCommand<string>(idx =>
            {
                if (int.TryParse(idx, out int parsed))
                {
                    SelectedTabIndex = Math.Clamp(parsed, 0, 2);
                }
            });
            GoBackCommand = new RelayCommand(GoBack);

            if (_consoleLogService != null)
            {
                _consoleLogService.LogReceived += OnConsoleLogReceived;
            }

            _processRunner.StateChanged += OnProcessStateChanged;
        }

        public void Initialize(ServerInstance instance)
        {
            SelectedInstance = instance;
            UpdateOnlineStatus();
            LoadWhitelistEnabledState();
            SetupWatcher();
            _ = LoadListsAsync();

            try
            {
                _timer?.Stop();
                _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.0) };
                _timer.Tick += (_, _) =>
                {
                    UpdateLastUpdatedText();
                    _ = LoadListsAsync();
                };
                _timer.Start();
            }
            catch
            {
                // Headless unit test environment without UI dispatcher loop.
            }
        }

        public void LoadLists()
        {
            if (SelectedInstance == null) return;

            UpdateOnlineStatus();
            LoadWhitelistEnabledState();

            var online = _playerService.GetOnlinePlayersAsync(SelectedInstance).GetAwaiter().GetResult();
            var wl = _playerService.GetWhitelistAsync(SelectedInstance).GetAwaiter().GetResult();
            var opsList = _playerService.GetOpsAsync(SelectedInstance).GetAwaiter().GetResult();
            var bannedEntries = LoadBannedEntries();

            lock (_collectionLock)
            {
                ApplyCollections(online, wl, opsList, bannedEntries);
            }
        }

        public async Task LoadListsAsync()
        {
            if (SelectedInstance == null) return;

            await _loadLock.WaitAsync();
            try
            {
                var online = await _playerService.GetOnlinePlayersAsync(SelectedInstance);
                var wl = await _playerService.GetWhitelistAsync(SelectedInstance);
                var opsList = await _playerService.GetOpsAsync(SelectedInstance);
                var bannedEntries = await Task.Run(LoadBannedEntries);

                PostToUi(() =>
                {
                    lock (_collectionLock)
                    {
                        UpdateOnlineStatus();
                        LoadWhitelistEnabledState();
                        ApplyCollections(online, wl, opsList, bannedEntries);
                    }
                });
            }
            finally
            {
                _loadLock.Release();
            }
        }

        private async Task RefreshAllAsync()
        {
            IsRefreshingState = true;
            try
            {
                await SendConsoleCommand("list");
                await LoadListsAsync();
            }
            finally
            {
                IsRefreshingState = false;
            }
        }

        private void ApplyCollections(
            IReadOnlyList<string> online,
            IReadOnlyList<string> wl,
            IReadOnlyList<string> opsList,
            IReadOnlyList<BannedPlayerEntry> bannedEntries)
        {
            var opSet = new HashSet<string>(opsList, StringComparer.OrdinalIgnoreCase);
            var banNames = bannedEntries.Select(b => b.Name).Where(n => !string.IsNullOrEmpty(n)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var banSet = new HashSet<string>(banNames, StringComparer.OrdinalIgnoreCase);

            OnlinePlayers.Clear();
            foreach (var p in online) OnlinePlayers.Add(p);

            Whitelist.Clear();
            foreach (var p in wl) Whitelist.Add(p);

            Ops.Clear();
            foreach (var p in opsList) Ops.Add(p);

            BannedPlayers.Clear();
            foreach (var p in banNames) BannedPlayers.Add(p);

            var existingCards = new Dictionary<string, PlayerViewModel>(StringComparer.OrdinalIgnoreCase);
            foreach (var existing in OnlinePlayerCards)
            {
                if (existing != null && !string.IsNullOrEmpty(existing.Name))
                {
                    existingCards[existing.Name] = existing;
                }
            }
            OnlinePlayerCards.Clear();
            foreach (var name in online)
            {
                if (existingCards.TryGetValue(name, out var card))
                {
                    card.IsOp = opSet.Contains(name);
                    card.IsBanned = banSet.Contains(name);
                    card.IsServerOnline = IsServerOnline;
                }
                else
                {
                    card = new PlayerViewModel(
                        name,
                        TogglePlayerOpAsync,
                        ChangePlayerGamemodeAsync,
                        SubmitPlayerReasonAsync)
                    {
                        IsOp = opSet.Contains(name),
                        IsBanned = banSet.Contains(name),
                        IsServerOnline = IsServerOnline
                    };
                }

                OnlinePlayerCards.Add(card);
            }

            BanListEntries.Clear();
            foreach (var entry in bannedEntries)
            {
                BanListEntries.Add(new BannedPlayerViewModel(
                    entry.Name,
                    entry.Reason,
                    entry.Created,
                    entry.Expires,
                    entry.IsSidecar,
                    IsServerOnline,
                    b => UnbanPlayerAsync(b.Name)));
            }

            WhitelistEntries.Clear();
            foreach (var name in wl)
            {
                WhitelistEntries.Add(new WhitelistPlayerViewModel(name, n => RemoveWhitelistPlayerAsync(n)));
            }

            _lastUpdatedUtc = DateTime.UtcNow;
            UpdateLastUpdatedText();

            OnPropertyChanged(nameof(HasOnlinePlayers));
            OnPropertyChanged(nameof(HasBannedPlayers));
            OnPropertyChanged(nameof(HasWhitelistedPlayers));
        }

        private List<BannedPlayerEntry> LoadBannedEntries()
        {
            var entries = new List<BannedPlayerEntry>();
            if (SelectedInstance == null) return entries;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _fileLock.Wait();
            try
            {
                var jsonPath = Path.Combine(SelectedInstance.Path, "banned-players.json");
                if (File.Exists(jsonPath))
                {
                    try
                    {
                        var content = File.ReadAllText(jsonPath);
                        using var doc = JsonDocument.Parse(content);
                        if (doc.RootElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var el in doc.RootElement.EnumerateArray())
                            {
                                string name = el.TryGetProperty("name", out var nameProp) ? (nameProp.GetString() ?? string.Empty) : string.Empty;
                                if (string.IsNullOrWhiteSpace(name) || !seen.Add(name)) continue;

                                string reason = el.TryGetProperty("reason", out var reasonProp) ? (reasonProp.GetString() ?? string.Empty) : string.Empty;
                                string source = el.TryGetProperty("source", out var sourceProp) ? (sourceProp.GetString() ?? string.Empty) : string.Empty;
                                string expires = el.TryGetProperty("expires", out var expiresProp) ? (expiresProp.GetString() ?? "forever") : "forever";
                                string created = el.TryGetProperty("created", out var createdProp) ? (createdProp.GetString() ?? string.Empty) : string.Empty;

                                entries.Add(new BannedPlayerEntry
                                {
                                    Name = name,
                                    Reason = reason,
                                    Created = !string.IsNullOrWhiteSpace(source) ? source : created,
                                    Expires = expires,
                                    IsSidecar = false
                                });
                            }
                        }
                    }
                    catch { }
                }

                foreach (string txtName in new[] { "banned-players.txt", "banned.txt" })
                {
                    var txtPath = Path.Combine(SelectedInstance.Path, txtName);
                    if (!File.Exists(txtPath)) continue;

                    try
                    {
                        foreach (var line in File.ReadAllLines(txtPath))
                        {
                            var trimmed = line.Trim();
                            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#", StringComparison.Ordinal))
                                continue;

                            var parts = trimmed.Split('|');
                            var name = parts[0].Trim();
                            if (string.IsNullOrWhiteSpace(name) || !seen.Add(name)) continue;

                            entries.Add(new BannedPlayerEntry
                            {
                                Name = name,
                                Created = parts.Length > 1 ? parts[1].Trim() : string.Empty,
                                Expires = parts.Length > 3 ? parts[3].Trim() : "forever",
                                Reason = parts.Length > 4 ? parts[4].Trim() : string.Empty,
                                IsSidecar = false
                            });
                        }
                    }
                    catch { }
                }

                if (SelectedInstance.EngineType == EngineType.Bedrock)
                {
                    try
                    {
                        var sidecarBans = _banSidecarService.GetBannedPlayersAsync(SelectedInstance.Path).GetAwaiter().GetResult();
                        foreach (var b in sidecarBans)
                        {
                            if (!string.IsNullOrWhiteSpace(b.Name) && seen.Add(b.Name))
                            {
                                entries.Add(b);
                            }
                        }
                    }
                    catch { }
                }

                return entries;
            }
            finally
            {
                _fileLock.Release();
            }
        }

        private void UpdateOnlineStatus()
        {
            if (SelectedInstance == null)
            {
                IsServerOnline = false;
                return;
            }

            bool hasInfo = _processRunner.TryGetRunningInfo(SelectedInstance.Slug, out _, out string state);
            IsServerOnline = hasInfo &&
                             (string.Equals(state, "Running", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(state, "Online", StringComparison.OrdinalIgnoreCase));
        }

        private void UpdateLastUpdatedText()
        {
            if (_lastUpdatedUtc == null)
            {
                LastUpdatedText = "Waiting for player list";
                return;
            }

            int seconds = Math.Max(0, (int)(DateTime.UtcNow - _lastUpdatedUtc.Value).TotalSeconds);
            LastUpdatedText = seconds < 3 ? "Updated just now" : $"Updated {seconds}s ago";
        }

        private void LoadWhitelistEnabledState()
        {
            if (SelectedInstance == null || string.IsNullOrWhiteSpace(SelectedInstance.Path))
            {
                IsWhitelistEnabled = false;
                return;
            }

            string propsPath = Path.Combine(SelectedInstance.Path, "server.properties");
            if (!File.Exists(propsPath))
            {
                IsWhitelistEnabled = false;
                return;
            }

            try
            {
                string key = SelectedInstance.EngineType == EngineType.Bedrock ? "allow-list" : "white-list";
                foreach (string rawLine in File.ReadAllLines(propsPath))
                {
                    string line = rawLine.Trim();
                    if (line.StartsWith("#", StringComparison.Ordinal) || !line.Contains('=')) continue;
                    int eq = line.IndexOf('=');
                    string k = line.Substring(0, eq).Trim();
                    if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(k, "white-list", StringComparison.OrdinalIgnoreCase))
                    {
                        IsWhitelistEnabled = string.Equals(line.Substring(eq + 1).Trim(), "true", StringComparison.OrdinalIgnoreCase);
                        return;
                    }
                }
            }
            catch
            {
            }
        }

        private async Task ToggleWhitelistAsync()
        {
            if (SelectedInstance == null || string.IsNullOrWhiteSpace(SelectedInstance.Path)) return;

            bool nextState = !IsWhitelistEnabled;
            string propsPath = Path.Combine(SelectedInstance.Path, "server.properties");
            string key = SelectedInstance.EngineType == EngineType.Bedrock ? "allow-list" : "white-list";

            await _fileLock.WaitAsync();
            try
            {
                var lines = File.Exists(propsPath) ? File.ReadAllLines(propsPath).ToList() : new List<string>();
                bool updated = false;
                for (int i = 0; i < lines.Count; i++)
                {
                    string trimmed = lines[i].Trim();
                    if (trimmed.StartsWith("#", StringComparison.Ordinal) || !trimmed.Contains('=')) continue;
                    int eq = trimmed.IndexOf('=');
                    if (string.Equals(trimmed.Substring(0, eq).Trim(), key, StringComparison.OrdinalIgnoreCase))
                    {
                        lines[i] = $"{key}={(nextState ? "true" : "false")}";
                        updated = true;
                        break;
                    }
                }

                if (!updated)
                {
                    lines.Add($"{key}={(nextState ? "true" : "false")}");
                }

                await File.WriteAllLinesAsync(propsPath, lines);
                IsWhitelistEnabled = nextState;
            }
            finally
            {
                _fileLock.Release();
            }

            if (IsServerOnline && SelectedInstance.EngineType != EngineType.Bedrock)
            {
                await SendConsoleCommand(nextState ? "whitelist on" : "whitelist off");
            }
        }

        private async Task ReloadWhitelistAsync()
        {
            if (!IsServerOnline || SelectedInstance == null) return;

            string cmd = SelectedInstance.EngineType == EngineType.Bedrock ? "allowlist reload" : "whitelist reload";
            await SendConsoleCommand(cmd);
            await LoadListsAsync();
        }

        private async Task TogglePlayerOpAsync(PlayerViewModel player)
        {
            if (SelectedInstance == null || string.IsNullOrWhiteSpace(player.Name)) return;

            bool targetOp = !player.IsOp;
            string formatted = CommandFormatter.FormatPlayerName(player.Name, SelectedInstance.EngineType.ToString());
            if (targetOp)
            {
                await _playerService.AddOpAsync(SelectedInstance, player.Name);
                await SendConsoleCommand($"op {formatted}");
            }
            else
            {
                await _playerService.RemoveOpAsync(SelectedInstance, player.Name);
                await SendConsoleCommand($"deop {formatted}");
            }

            await LoadListsAsync();
        }

        private async Task ChangePlayerGamemodeAsync(PlayerViewModel player, string mode)
        {
            if (SelectedInstance == null || string.IsNullOrWhiteSpace(player.Name)) return;

            string formatted = CommandFormatter.FormatPlayerName(player.Name, SelectedInstance.EngineType.ToString());
            await SendConsoleCommand($"gamemode {mode} {formatted}");
            player.SetGameModeFromServer(mode);
        }

        private async Task<bool> SubmitPlayerReasonAsync(PlayerViewModel player, string action, string reason)
        {
            if (SelectedInstance == null || string.IsNullOrWhiteSpace(player.Name))
            {
                return false;
            }

            string serverType = SelectedInstance.EngineType.ToString();
            string formatted = CommandFormatter.FormatPlayerName(player.Name, serverType);
            IReadOnlyList<string> commands = PlayerActionCommandBuilder.BuildSubmitCommands(action, formatted, serverType, reason);

            foreach (string cmd in commands)
            {
                await SendConsoleCommand(cmd);
            }

            if (string.Equals(action, "Ban", StringComparison.OrdinalIgnoreCase))
            {
                if (SelectedInstance.EngineType == EngineType.Bedrock)
                {
                    await _banSidecarService.AddBanAsync(
                        SelectedInstance.Path,
                        player.Name,
                        string.IsNullOrWhiteSpace(reason) ? "Banned by administrator" : reason);
                }
                else if (!IsServerOnline)
                {
                    var txtPath = Path.Combine(SelectedInstance.Path, "banned-players.txt");
                    await File.AppendAllTextAsync(txtPath, player.Name + Environment.NewLine);
                }
            }

            await LoadListsAsync();
            return true;
        }

        private void SetupWatcher()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
                _watcher = null;
            }

            if (SelectedInstance == null || !Directory.Exists(SelectedInstance.Path)) return;

            _watcher = new FileSystemWatcher(SelectedInstance.Path)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                Filter = "*.*",
                EnableRaisingEvents = true
            };

            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileChanged;
            _watcher.Deleted += OnFileChanged;
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            var name = Path.GetFileName(e.FullPath).ToLowerInvariant();
            if (name.Contains("whitelist") || name.Contains("allowlist") || name.Contains("ops") || name.Contains("banned") || name.Contains("permissions"))
            {
                _ = LoadListsAsync();
            }
        }

        private void OnConsoleLogReceived(string slug, string line)
        {
            if (SelectedInstance == null || !string.Equals(slug, SelectedInstance.Slug, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (PlayerGamemodeLogParser.TryParse(line, out var ev) && ev != null)
            {
                PostToUi(() =>
                {
                    var card = OnlinePlayerCards.FirstOrDefault(p => string.Equals(p.Name, ev.PlayerName, StringComparison.OrdinalIgnoreCase));
                    card?.SetGameModeFromServer(ev.Gamemode);
                });
            }
        }

        private void OnProcessStateChanged(string slug, string state)
        {
            if (SelectedInstance != null && string.Equals(slug, SelectedInstance.Slug, StringComparison.OrdinalIgnoreCase))
            {
                PostToUi(() =>
                {
                    UpdateOnlineStatus();
                    foreach (var card in OnlinePlayerCards)
                    {
                        card.IsServerOnline = IsServerOnline;
                    }
                    foreach (var ban in BanListEntries)
                    {
                        ban.IsServerOnline = IsServerOnline;
                    }
                });
            }
        }

        private async Task SendConsoleCommand(string cmd)
        {
            if (SelectedInstance == null) return;
            bool isRunning = _processRunner.TryGetRunningInfo(SelectedInstance.Slug, out _, out string state);
            if (isRunning && (state == "Running" || state == "Online"))
            {
                await _processRunner.SendCommandAsync(SelectedInstance, cmd);
            }
        }

        private async Task KickPlayerAsync(string? username)
        {
            if (string.IsNullOrWhiteSpace(username)) return;
            await SendConsoleCommand($"kick {username}");
            await LoadListsAsync();
        }

        private async Task BanPlayerAsync(string? username)
        {
            var target = username ?? NewBanPlayer;
            if (string.IsNullOrWhiteSpace(target)) return;
            await SendConsoleCommand($"ban {target}");

            if (SelectedInstance != null)
            {
                if (SelectedInstance.EngineType == EngineType.Bedrock)
                {
                    await _banSidecarService.AddBanAsync(SelectedInstance.Path, target, "Banned by administrator");
                }
                else
                {
                    bool isRunning = _processRunner.TryGetRunningInfo(SelectedInstance.Slug, out _, out string state);
                    if (!isRunning || state != "Running")
                    {
                        await _fileLock.WaitAsync();
                        try
                        {
                            var txtPath = Path.Combine(SelectedInstance.Path, "banned-players.txt");
                            await File.AppendAllTextAsync(txtPath, target + Environment.NewLine);
                        }
                        finally
                        {
                            _fileLock.Release();
                        }
                    }
                }
            }

            NewBanPlayer = string.Empty;
            await LoadListsAsync();
        }

        private async Task UnbanPlayerAsync(string? username)
        {
            if (string.IsNullOrWhiteSpace(username)) return;
            await SendConsoleCommand($"pardon {username}");

            if (SelectedInstance != null)
            {
                if (SelectedInstance.EngineType == EngineType.Bedrock)
                {
                    await _banSidecarService.RemoveBanAsync(SelectedInstance.Path, username);
                }
                else
                {
                    bool isRunning = _processRunner.TryGetRunningInfo(SelectedInstance.Slug, out _, out string state);
                    if (!isRunning || state != "Running")
                    {
                        await _fileLock.WaitAsync();
                        try
                        {
                            var txtPath = Path.Combine(SelectedInstance.Path, "banned-players.txt");
                            if (File.Exists(txtPath))
                            {
                                var lines = File.ReadAllLines(txtPath)
                                    .Where(l => !l.Trim().Equals(username, StringComparison.OrdinalIgnoreCase))
                                    .ToList();
                                File.WriteAllLines(txtPath, lines);
                            }
                        }
                        finally
                        {
                            _fileLock.Release();
                        }
                    }
                }
            }

            await LoadListsAsync();
        }

        private async Task AddWhitelistPlayerAsync()
        {
            if (string.IsNullOrWhiteSpace(NewWhitelistPlayer) || SelectedInstance == null) return;
            var target = NewWhitelistPlayer;
            NewWhitelistPlayer = string.Empty;

            await _playerService.AddWhitelistAsync(SelectedInstance, target);
            await SendConsoleCommand($"whitelist add {target}");
            await SendConsoleCommand("whitelist reload");
            await LoadListsAsync();
        }

        private async Task RemoveWhitelistPlayerAsync(string? username)
        {
            if (string.IsNullOrWhiteSpace(username) || SelectedInstance == null) return;
            await _playerService.RemoveWhitelistAsync(SelectedInstance, username);
            await SendConsoleCommand($"whitelist remove {username}");
            await SendConsoleCommand("whitelist reload");
            await LoadListsAsync();
        }

        private async Task AddOpPlayerAsync()
        {
            if (string.IsNullOrWhiteSpace(NewOpPlayer) || SelectedInstance == null) return;
            var target = NewOpPlayer;
            NewOpPlayer = string.Empty;

            await _playerService.AddOpAsync(SelectedInstance, target);
            await SendConsoleCommand($"op {target}");
            await LoadListsAsync();
        }

        private async Task RemoveOpPlayerAsync(string? username)
        {
            if (string.IsNullOrWhiteSpace(username) || SelectedInstance == null) return;
            await _playerService.RemoveOpAsync(SelectedInstance, username);
            await SendConsoleCommand($"deop {username}");
            await LoadListsAsync();
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

        public void Dispose()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer = null;
            }
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
                _watcher = null;
            }
            if (_consoleLogService != null)
            {
                _consoleLogService.LogReceived -= OnConsoleLogReceived;
            }
            _processRunner.StateChanged -= OnProcessStateChanged;
            GC.SuppressFinalize(this);
        }
    }
}
