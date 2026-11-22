using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PocketMC.Core.Services;

namespace PocketMC.Platform.Lifecycle;

public sealed class SingleInstanceService : ISingleInstanceService
{
    private readonly string _socketPath;
    private readonly ILogger<SingleInstanceService>? _logger;
    private Socket? _listenerSocket;
    private CancellationTokenSource? _cts;
    private bool _isFirstInstance;
    private bool _disposed;

    public bool IsFirstInstance => _isFirstInstance;

    public SingleInstanceService(string? customSocketPath = null, ILogger<SingleInstanceService>? logger = null)
    {
        _socketPath = customSocketPath ?? GetDefaultSocketPath();
        _logger = logger;
    }

    public static string GetDefaultSocketPath()
    {
        string username = Environment.UserName;
        if (string.IsNullOrWhiteSpace(username))
        {
            username = "default";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Path.Combine(Path.GetTempPath(), $"pocketmc_{username}.sock");
        }

        return $"/tmp/pocketmc_{username}.sock";
    }

    public bool TryAcquireLock()
    {
        if (_disposed) return false;

        // Try to connect to existing socket
        try
        {
            if (File.Exists(_socketPath))
            {
                using var testClient = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                var endpoint = new UnixDomainSocketEndPoint(_socketPath);
                testClient.Connect(endpoint);

                // Existing instance is alive
                _isFirstInstance = false;
                return false;
            }
        }
        catch
        {
            // Socket exists on disk but connection failed — stale socket from previous crash.
            // Clean it up.
            try
            {
                if (File.Exists(_socketPath))
                {
                    File.Delete(_socketPath);
                }
            }
            catch
            {
                // Best effort
            }
        }

        try
        {
            _listenerSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            var endpoint = new UnixDomainSocketEndPoint(_socketPath);
            _listenerSocket.Bind(endpoint);
            _listenerSocket.Listen(5);

            _isFirstInstance = true;
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to bind single instance socket '{Path}'.", _socketPath);
            _isFirstInstance = false;
            return false;
        }
    }

    public void StartListening(Action<string[]> onArgumentsReceived)
    {
        if (!_isFirstInstance || _listenerSocket == null || _disposed)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested && !_disposed)
            {
                try
                {
                    var client = await _listenerSocket.AcceptAsync(token);
                    _ = HandleClientAsync(client, onArgumentsReceived, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (_disposed) break;
                    _logger?.LogDebug(ex, "Error accepting client on single-instance socket.");
                }
            }
        }, token);
    }

    private static async Task HandleClientAsync(Socket client, Action<string[]> onArgumentsReceived, CancellationToken token)
    {
        using (client)
        {
            try
            {
                using var stream = new NetworkStream(client, ownsSocket: false);
                using var reader = new StreamReader(stream, Encoding.UTF8);

                string? json = await reader.ReadLineAsync(token);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var args = JsonSerializer.Deserialize<string[]>(json);
                    if (args != null)
                    {
                        onArgumentsReceived(args);
                    }
                }
            }
            catch
            {
                // Client communication failed
            }
        }
    }

    public async Task<bool> SendArgumentsToExistingInstanceAsync(string[] args)
    {
        try
        {
            using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            var endpoint = new UnixDomainSocketEndPoint(_socketPath);
            await client.ConnectAsync(endpoint);

            using var stream = new NetworkStream(client, ownsSocket: false);
            using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            string json = JsonSerializer.Serialize(args);
            await writer.WriteLineAsync(json);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to send arguments to existing PocketMC instance.");
            return false;
        }
    }

    public void RegisterUrlProtocol()
    {
        try
        {
            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                return;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                RegisterLinuxDesktopEntry(exePath);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                RegisterWindowsProtocol(exePath);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to register custom URL protocol.");
        }
    }

    private static void RegisterLinuxDesktopEntry(string exePath)
    {
        try
        {
            string appsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local", "share", "applications");
            Directory.CreateDirectory(appsDir);

            string desktopFilePath = Path.Combine(appsDir, "pocketmc.desktop");
            string content = $@"[Desktop Entry]
Name=PocketMC
Comment=Minecraft Server Manager
Exec=""{exePath}"" %u
Icon=pocketmc
Terminal=false
Type=Application
Categories=Game;Utility;
MimeType=x-scheme-handler/pocketmc;
";
            File.WriteAllText(desktopFilePath, content);

            // Register default scheme handler via xdg-mime if available
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "xdg-mime",
                    Arguments = "default pocketmc.desktop x-scheme-handler/pocketmc",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(2000);
            }
            catch
            {
                // xdg-mime might not be installed
            }
        }
        catch
        {
            // Non-critical
        }
    }

    private static void RegisterWindowsProtocol(string exePath)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        try
        {
            using var classesKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Classes", true);
            if (classesKey == null) return;

            using var schemeKey = classesKey.CreateSubKey("pocketmc");
            if (schemeKey == null) return;

            schemeKey.SetValue("", "PocketMC URL Protocol");
            schemeKey.SetValue("URL Protocol", "");

            using var defaultIconKey = schemeKey.CreateSubKey("DefaultIcon");
            defaultIconKey?.SetValue("", $"\"{exePath}\",0");

            using var commandKey = schemeKey.CreateSubKey(@"shell\open\command");
            commandKey?.SetValue("", $"\"{exePath}\" \"%1\"");
        }
        catch
        {
            // Non-critical
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _listenerSocket?.Close();
            _listenerSocket?.Dispose();
            _listenerSocket = null;

            if (_isFirstInstance && File.Exists(_socketPath))
            {
                File.Delete(_socketPath);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }
}
