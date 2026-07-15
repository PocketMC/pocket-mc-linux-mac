using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PocketMC.Core.Models;
using PocketMC.Core.Services;

namespace PocketMC.App.ViewModels
{
    public partial class PlayerManagementViewModel : ObservableObject, IDisposable
    {
        private readonly IInstanceService _instanceService;
        private readonly IPlayerService _playerService;
        private readonly IProcessRunner _processRunner;

        private FileSystemWatcher? _watcher;

        [ObservableProperty]
        private ServerInstance? _selectedInstance;

        [ObservableProperty] private string _newWhitelistPlayer = string.Empty;
        [ObservableProperty] private string _newBanPlayer = string.Empty;
        [ObservableProperty] private string _newOpPlayer = string.Empty;

        public ObservableCollection<string> OnlinePlayers { get; } = new();
        public ObservableCollection<string> Whitelist { get; } = new();
        public ObservableCollection<string> Ops { get; } = new();
        public ObservableCollection<string> BannedPlayers { get; } = new();

        public IAsyncRelayCommand<string> KickPlayerCommand { get; }
        public IAsyncRelayCommand<string> BanPlayerCommand { get; }
        public IAsyncRelayCommand<string> UnbanPlayerCommand { get; }
        public IAsyncRelayCommand AddWhitelistPlayerCommand { get; }
        public IAsyncRelayCommand<string> RemoveWhitelistPlayerCommand { get; }
        public IAsyncRelayCommand AddOpPlayerCommand { get; }
        public IAsyncRelayCommand<string> RemoveOpPlayerCommand { get; }
        public IRelayCommand GoBackCommand { get; }

        public PlayerManagementViewModel(
            IInstanceService instanceService,
            IPlayerService playerService,
            IProcessRunner processRunner)
        {
            _instanceService = instanceService;
            _playerService = playerService;
            _processRunner = processRunner;

            KickPlayerCommand = new AsyncRelayCommand<string>(KickPlayerAsync);
            BanPlayerCommand = new AsyncRelayCommand<string>(BanPlayerAsync);
            UnbanPlayerCommand = new AsyncRelayCommand<string>(UnbanPlayerAsync);
            AddWhitelistPlayerCommand = new AsyncRelayCommand(AddWhitelistPlayerAsync);
            RemoveWhitelistPlayerCommand = new AsyncRelayCommand<string>(RemoveWhitelistPlayerAsync);
            AddOpPlayerCommand = new AsyncRelayCommand(AddOpPlayerAsync);
            RemoveOpPlayerCommand = new AsyncRelayCommand<string>(RemoveOpPlayerAsync);
            GoBackCommand = new RelayCommand(GoBack);
        }

        private DispatcherTimer? _timer;

        public void Initialize(ServerInstance instance)
        {
            SelectedInstance = instance;
            SetupWatcher();
            _ = LoadListsAsync();

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2.0)
            };
            _timer.Tick += (s, e) => { _ = LoadListsAsync(); };
            _timer.Start();
        }

        public void LoadLists()
        {
            if (SelectedInstance == null) return;

            // Load online players
            var online = _playerService.GetOnlinePlayersAsync(SelectedInstance).GetAwaiter().GetResult();
            OnlinePlayers.Clear();
            foreach (var p in online) OnlinePlayers.Add(p);

            // Load whitelist
            var wl = _playerService.GetWhitelistAsync(SelectedInstance).GetAwaiter().GetResult();
            Whitelist.Clear();
            foreach (var p in wl) Whitelist.Add(p);

            // Load ops
            var opsList = _playerService.GetOpsAsync(SelectedInstance).GetAwaiter().GetResult();
            Ops.Clear();
            foreach (var p in opsList) Ops.Add(p);

            // Load banned players
            var banned = LoadBannedPlayers();
            BannedPlayers.Clear();
            foreach (var p in banned) BannedPlayers.Add(p);
        }

        public async Task LoadListsAsync()
        {
            if (SelectedInstance == null) return;

            var online = await _playerService.GetOnlinePlayersAsync(SelectedInstance);
            var wl = await _playerService.GetWhitelistAsync(SelectedInstance);
            var opsList = await _playerService.GetOpsAsync(SelectedInstance);
            var banned = await Task.Run(() => LoadBannedPlayers());

            Dispatcher.UIThread.Post(() =>
            {
                OnlinePlayers.Clear();
                foreach (var p in online) OnlinePlayers.Add(p);

                Whitelist.Clear();
                foreach (var p in wl) Whitelist.Add(p);

                Ops.Clear();
                foreach (var p in opsList) Ops.Add(p);

                BannedPlayers.Clear();
                foreach (var p in banned) BannedPlayers.Add(p);
            });
        }

        private List<string> LoadBannedPlayers()
        {
            var banned = new List<string>();
            if (SelectedInstance == null) return banned;

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
                            if (el.TryGetProperty("name", out var nameProp))
                            {
                                banned.Add(nameProp.GetString() ?? "");
                            }
                        }
                    }
                }
                catch { }
            }

            var txtPath = Path.Combine(SelectedInstance.Path, "banned-players.txt");
            if (File.Exists(txtPath))
            {
                try
                {
                    var lines = File.ReadAllLines(txtPath);
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith("#"))
                        {
                            banned.Add(trimmed);
                        }
                    }
                }
                catch { }
            }

            return banned.Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList();
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
            if (name.Contains("whitelist") || name.Contains("ops") || name.Contains("banned-players") || name.Contains("permissions"))
            {
                _ = LoadListsAsync();
            }
        }

        private async Task SendConsoleCommand(string cmd)
        {
            if (SelectedInstance == null) return;
            bool isRunning = _processRunner.TryGetRunningInfo(SelectedInstance.Slug, out _, out string state);
            if (isRunning && state == "Running")
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
                bool isRunning = _processRunner.TryGetRunningInfo(SelectedInstance.Slug, out _, out string state);
                if (!isRunning || state != "Running")
                {
                    var txtPath = Path.Combine(SelectedInstance.Path, "banned-players.txt");
                    await File.AppendAllTextAsync(txtPath, target + Environment.NewLine);
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
                bool isRunning = _processRunner.TryGetRunningInfo(SelectedInstance.Slug, out _, out string state);
                if (!isRunning || state != "Running")
                {
                    var txtPath = Path.Combine(SelectedInstance.Path, "banned-players.txt");
                    if (File.Exists(txtPath))
                    {
                        var lines = File.ReadAllLines(txtPath).Where(l => !l.Trim().Equals(username, StringComparison.OrdinalIgnoreCase));
                        File.WriteAllLines(txtPath, lines);
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
            GC.SuppressFinalize(this);
        }
    }
}
