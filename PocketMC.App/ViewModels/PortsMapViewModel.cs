using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.RemoteControl.Services;
using PocketMC.RemoteControl.Tunnels;

namespace PocketMC.App.ViewModels
{
    public partial class PortsMapViewModel : ObservableObject
    {
        private readonly IInstanceService _instanceService;
        private readonly IProcessRunner _processRunner;
        private readonly PlayitApiClient _playitApiClient;
        private readonly ISettingsService _settingsService;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private ObservableCollection<ServerGroupViewModel> _serverGroups = new();

        public PortsMapViewModel(
            IInstanceService instanceService,
            IProcessRunner processRunner,
            PlayitApiClient playitApiClient,
            ISettingsService settingsService)
        {
            _instanceService = instanceService;
            _processRunner = processRunner;
            _playitApiClient = playitApiClient;
            _settingsService = settingsService;
        }

        [RelayCommand]
        public async Task RefreshMapAsync()
        {
            IsLoading = true;
            StatusMessage = "Discovering server ports and active network routes...";

            try
            {
                var instances = await _instanceService.ListInstancesAsync();
                var tunnelsResult = await _playitApiClient.GetTunnelsAsync();
                var tunnels = tunnelsResult.Success ? tunnelsResult.Tunnels : new List<TunnelData>();

                var groups = new List<ServerGroupViewModel>();

                foreach (var inst in instances)
                {
                    bool isOnline = _processRunner.TryGetRunningInfo(inst.Slug, out _, out var state) &&
                                    string.Equals(state, "running", StringComparison.OrdinalIgnoreCase);

                    var group = new ServerGroupViewModel
                    {
                        ServerId = inst.Id,
                        ServerName = inst.Name,
                        ServerInfo = $"{inst.EngineType} {inst.EngineVersion}",
                        IsOnline = isOnline,
                        IsExpanded = isOnline || instances.Count == 1,
                        ActiveColorHex = isOnline ? "#00E676" : "#888888"
                    };

                    var routes = DiscoverInstanceRoutes(inst, isOnline, tunnels);
                    foreach (var route in routes)
                    {
                        group.Routes.Add(route);
                    }

                    groups.Add(group);
                }

                ServerGroups.Clear();
                foreach (var g in groups)
                {
                    ServerGroups.Add(g);
                }

                StatusMessage = ServerGroups.Count == 0 ? "No server instances found." : string.Empty;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to discover network topology: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private List<PortRouteViewModel> DiscoverInstanceRoutes(ServerInstance inst, bool isOnline, List<TunnelData> tunnels)
        {
            var routes = new List<PortRouteViewModel>();
            if (string.IsNullOrEmpty(inst.Path) || !Directory.Exists(inst.Path))
            {
                return routes;
            }

            var serverPropsPath = Path.Combine(inst.Path, "server.properties");
            var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(serverPropsPath))
            {
                properties = ServerPropertiesHelper.ReadProperties(serverPropsPath);
            }

            bool isBedrockFamily = inst.EngineType == EngineType.Bedrock || inst.EngineType == EngineType.PocketMine;

            // 1. Primary Game Port
            int gamePort = isBedrockFamily ? 19132 : 25565;
            if (properties.TryGetValue("server-port", out var spStr) && int.TryParse(spStr, out int sp))
            {
                gamePort = sp;
            }
            string gameProtocol = isBedrockFamily ? "UDP" : "TCP";
            routes.Add(CreateRoute(
                inst,
                gamePort,
                gameProtocol,
                "GamePort",
                isBedrockFamily ? "Minecraft Bedrock Port" : "Minecraft Java Port",
                isBedrockFamily ? "Standard Bedrock dedicated server endpoint" : "Standard Java server endpoint",
                isOnline,
                tunnels));

            // 2. RCON Port (Java only, if enabled)
            if (!isBedrockFamily && properties.TryGetValue("enable-rcon", out var rconEnabled) && string.Equals(rconEnabled, "true", StringComparison.OrdinalIgnoreCase))
            {
                int rconPort = 25575;
                if (properties.TryGetValue("rcon.port", out var rpStr) && int.TryParse(rpStr, out int rp))
                {
                    rconPort = rp;
                }
                routes.Add(CreateRoute(
                    inst,
                    rconPort,
                    "TCP",
                    "RconPort",
                    "RCON Remote Management",
                    "Remote console administrative endpoint",
                    isOnline,
                    tunnels));
            }

            // 3. Query Port (if enabled)
            if (properties.TryGetValue("enable-query", out var queryEnabled) && string.Equals(queryEnabled, "true", StringComparison.OrdinalIgnoreCase))
            {
                int queryPort = gamePort;
                if (properties.TryGetValue("query.port", out var qpStr) && int.TryParse(qpStr, out int qp))
                {
                    queryPort = qp;
                }
                routes.Add(CreateRoute(
                    inst,
                    queryPort,
                    "UDP",
                    "QueryPort",
                    "Query Protocol Port",
                    "GameSpy4 status and server ping protocol",
                    isOnline,
                    tunnels));
            }

            // 4. Geyser Bedrock UDP Port (if installed or Bedrock crossplay configured)
            var geyserConfigPath = Path.Combine(inst.Path, "plugins", "Geyser-Spigot", "config.yml");
            if (File.Exists(geyserConfigPath))
            {
                int geyserPort = 19132;
                try
                {
                    var lines = File.ReadAllLines(geyserConfigPath);
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("port:", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = trimmed.Split(':', 2);
                            if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out int gp))
                            {
                                geyserPort = gp;
                                break;
                            }
                        }
                    }
                }
                catch { }

                routes.Add(CreateRoute(
                    inst,
                    geyserPort,
                    "UDP",
                    "GeyserPort",
                    "Geyser Crossplay UDP",
                    "Bedrock protocol translation endpoint for Java",
                    isOnline,
                    tunnels));
            }

            // 5. Simple Voice Chat UDP Port (if installed)
            var voiceChatPropsPath = Path.Combine(inst.Path, "plugins", "voicechat", "voicechat-server.properties");
            if (File.Exists(voiceChatPropsPath))
            {
                int voicePort = 24454;
                try
                {
                    var lines = File.ReadAllLines(voiceChatPropsPath);
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("port", StringComparison.OrdinalIgnoreCase) && trimmed.Contains('='))
                        {
                            var parts = trimmed.Split('=', 2);
                            if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out int vp))
                            {
                                voicePort = vp;
                                break;
                            }
                        }
                    }
                }
                catch { }

                routes.Add(CreateRoute(
                    inst,
                    voicePort,
                    "UDP",
                    "VoiceChatPort",
                    "Simple Voice Chat UDP",
                    "Proximity voice chat audio streaming socket",
                    isOnline,
                    tunnels));
            }

            return routes;
        }

        private PortRouteViewModel CreateRoute(
            ServerInstance inst,
            int port,
            string protocol,
            string portType,
            string roleName,
            string roleDetail,
            bool isOnline,
            List<TunnelData> tunnels)
        {
            var matchingTunnel = tunnels.FirstOrDefault(t => t.Port == port);
            bool hasTunnel = matchingTunnel != null;

            string publicAddress = matchingTunnel?.PublicAddress ?? string.Empty;
            string publicAddressLabel = hasTunnel
                ? (!string.IsNullOrEmpty(publicAddress) ? publicAddress : "Allocating Address...")
                : "No active Playit tunnel";

            return new PortRouteViewModel
            {
                ServerId = inst.Id,
                ServerName = inst.Name,
                Port = port,
                Protocol = protocol,
                PortType = portType,
                ServiceRoleName = roleName,
                ServiceRoleDetail = roleDetail,
                LocalPortLabel = $"{port} / {protocol}",
                PublicAddress = publicAddress,
                PublicAddressLabel = publicAddressLabel,
                HasTunnel = hasTunnel,
                IsOnline = isOnline,
                ActiveColorHex = isOnline ? "#00E676" : "#888888"
            };
        }

        [RelayCommand]
        public void EditPort(PortRouteViewModel? route)
        {
            if (route == null) return;
            if (route.IsOnline)
            {
                StatusMessage = $"Cannot edit port for '{route.ServerName}' while server is running. Stop the server first.";
                return;
            }

            foreach (var g in ServerGroups)
            {
                foreach (var r in g.Routes)
                {
                    if (r != route) r.IsEditingPort = false;
                }
            }

            route.EditingPortText = route.Port.ToString();
            route.IsEditingPort = true;
        }

        [RelayCommand]
        public void CancelEditPort(PortRouteViewModel? route)
        {
            if (route == null) return;
            route.IsEditingPort = false;
        }

        [RelayCommand]
        public async Task SavePortAsync(PortRouteViewModel? route)
        {
            if (route == null) return;
            if (!int.TryParse(route.EditingPortText, out int newPort) || newPort < 1 || newPort > 65535)
            {
                StatusMessage = "Please enter a valid port between 1 and 65535.";
                return;
            }

            if (route.IsOnline)
            {
                StatusMessage = "Cannot change port while server is running.";
                return;
            }

            var instances = await _instanceService.ListInstancesAsync();
            var targetInstance = instances.FirstOrDefault(i => i.Id == route.ServerId);
            if (targetInstance == null || string.IsNullOrEmpty(targetInstance.Path))
            {
                StatusMessage = "Instance directory not found.";
                return;
            }

            if (route.PortType == "GamePort" || route.PortType == "RconPort" || route.PortType == "QueryPort")
            {
                string propKey = route.PortType switch
                {
                    "GamePort" => "server-port",
                    "RconPort" => "rcon.port",
                    "QueryPort" => "query.port",
                    _ => "server-port"
                };
                ServerPropertiesHelper.UpdateProperty(targetInstance.Path, propKey, newPort.ToString());
            }

            route.Port = newPort;
            route.LocalPortLabel = $"{newPort} / {route.Protocol}";
            route.IsEditingPort = false;
            StatusMessage = $"Updated {route.ServiceRoleName} to port {newPort} successfully.";
        }

        [RelayCommand]
        public async Task CopyAddressAsync(string? address)
        {
            if (string.IsNullOrEmpty(address)) return;
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
            {
                var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(desktop.MainWindow);
                if (topLevel?.Clipboard != null)
                {
                    await topLevel.Clipboard.SetTextAsync(address);
                    StatusMessage = $"Copied {address} to clipboard.";
                }
            }
        }

        [RelayCommand]
        public void GoBack()
        {
            var mainVM = App.Services.GetService(typeof(MainWindowViewModel)) as MainWindowViewModel;
            var tunnelVM = App.Services.GetService(typeof(TunnelViewModel)) as TunnelViewModel;
            if (mainVM != null && tunnelVM != null)
            {
                mainVM.CurrentViewModel = tunnelVM;
            }
        }

        [RelayCommand]
        public void NavigateToConsole(Guid serverId)
        {
            var mainVM = App.Services.GetService(typeof(MainWindowViewModel)) as MainWindowViewModel;
            var consoleVM = App.Services.GetService(typeof(ServerConsoleViewModel)) as ServerConsoleViewModel;
            if (mainVM != null && consoleVM != null)
            {
                mainVM.CurrentViewModel = consoleVM;
            }
        }
    }

    public partial class ServerGroupViewModel : ObservableObject
    {
        public Guid ServerId { get; set; }
        public string ServerName { get; set; } = string.Empty;
        public string ServerInfo { get; set; } = string.Empty;
        public bool IsOnline { get; set; }

        [ObservableProperty]
        private bool _isExpanded;

        public string ActiveColorHex { get; set; } = "#888888";
        public ObservableCollection<PortRouteViewModel> Routes { get; set; } = new();
    }

    public partial class PortRouteViewModel : ObservableObject
    {
        public Guid ServerId { get; set; }
        public string ServerName { get; set; } = string.Empty;

        [ObservableProperty]
        private int _port;

        public string PortType { get; set; } = string.Empty;
        public string Protocol { get; set; } = "TCP";

        [ObservableProperty]
        private string _localPortLabel = string.Empty;

        public string PublicAddress { get; set; } = string.Empty;
        public string PublicAddressLabel { get; set; } = string.Empty;
        public bool HasTunnel { get; set; }
        public bool IsOnline { get; set; }
        public string ActiveColorHex { get; set; } = "#888888";
        public string ServiceRoleName { get; set; } = string.Empty;
        public string ServiceRoleDetail { get; set; } = string.Empty;

        [ObservableProperty]
        private bool _isEditingPort;

        [ObservableProperty]
        private string _editingPortText = string.Empty;
    }
}
