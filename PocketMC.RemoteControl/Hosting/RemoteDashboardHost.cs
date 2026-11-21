using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;
using PocketMC.RemoteControl.Models;
using PocketMC.RemoteControl.Services;
using PocketMC.RemoteControl.Tunnels;

namespace PocketMC.RemoteControl.Hosting;

public sealed class RemoteDashboardHost : IHostedService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ISettingsService _settingsService;
    private readonly IInstanceService _instanceService;
    private readonly IProcessRunner _processRunner;
    private readonly RemoteAuthenticationService _authService;
    private readonly RemoteAuditLogService _auditLogService;
    private readonly RemoteRequestLimiter _requestLimiter;
    private readonly RemoteConsoleWebSocketHandler _wsHandler;
    private readonly RemoteTunnelManager _tunnelManager;
    private readonly LocalNetworkAddressService _localNetworkAddressService;
    private readonly RemoteStatusService _statusService;
    private readonly RemoteInstanceControlService _instanceControlService;
    private readonly RemotePlayerActionService _playerActionService;
    private readonly IConsoleLogService? _consoleLogService;
    private readonly IBackupService? _backupService;
    private readonly ILogger<RemoteDashboardHost> _logger;

    private readonly InstanceOperationCoordinator _operations = new();
    private readonly ConcurrentDictionary<Guid, bool> _activeBackups = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _revokedSessions = new();
    private readonly SemaphoreSlim _startGate = new(1, 1);

    private WebApplication? _app;

    public RemoteDashboardHost(
        ISettingsService settingsService,
        IInstanceService instanceService,
        IProcessRunner processRunner,
        RemoteAuthenticationService authService,
        RemoteAuditLogService auditLogService,
        RemoteRequestLimiter requestLimiter,
        RemoteConsoleWebSocketHandler wsHandler,
        RemoteTunnelManager tunnelManager,
        LocalNetworkAddressService localNetworkAddressService,
        RemoteStatusService statusService,
        RemoteInstanceControlService instanceControlService,
        RemotePlayerActionService playerActionService,
        IConsoleLogService? consoleLogService = null,
        IBackupService? backupService = null,
        ILogger<RemoteDashboardHost>? logger = null)
    {
        _settingsService = settingsService;
        _instanceService = instanceService;
        _processRunner = processRunner;
        _authService = authService;
        _auditLogService = auditLogService;
        _requestLimiter = requestLimiter;
        _wsHandler = wsHandler;
        _tunnelManager = tunnelManager;
        _localNetworkAddressService = localNetworkAddressService;
        _statusService = statusService;
        _instanceControlService = instanceControlService;
        _playerActionService = playerActionService;
        _consoleLogService = consoleLogService;
        _backupService = backupService;
        _logger = logger ?? NullLogger<RemoteDashboardHost>.Instance;
    }

    public bool IsRunning => _app != null;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _startGate.WaitAsync(cancellationToken);
        try
        {
            if (_app != null || !_settingsService.Settings.RemoteControl.Enabled)
            {
                return;
            }

            await StartServerInternalAsync(_settingsService.Settings.RemoteControl.Port, cancellationToken);
        }
        finally
        {
            _startGate.Release();
        }
    }

    public async Task StartServerAsync(int port)
    {
        await _startGate.WaitAsync();
        try
        {
            await StopInternalAsync(CancellationToken.None);
            await StartServerInternalAsync(port, CancellationToken.None);
        }
        finally
        {
            _startGate.Release();
        }
    }

    private async Task StartServerInternalAsync(int port, CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://*:{port}");

        _app = builder.Build();
        _app.UseWebSockets();

        string webRoot = Path.Combine(AppContext.BaseDirectory, "Web");
        if (Directory.Exists(webRoot))
        {
            _app.UseDefaultFiles(new DefaultFilesOptions
            {
                FileProvider = new PhysicalFileProvider(webRoot)
            });
            _app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(webRoot),
                OnPrepareResponse = ctx =>
                {
                    var path = ctx.Context.Request.Path.Value ?? string.Empty;
                    if (path.EndsWith(".html", StringComparison.OrdinalIgnoreCase) || path.EndsWith("/", StringComparison.OrdinalIgnoreCase))
                    {
                        ctx.Context.Response.Headers["Cache-Control"] = "no-cache";
                    }
                    else
                    {
                        ctx.Context.Response.Headers["Cache-Control"] = "public, max-age=86400";
                    }
                }
            });
        }

        _app.MapGet("/", () => Results.Redirect("/index.html"));
        _app.MapGet("/remote/index.html", () => Results.Redirect("/index.html"));
        _app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        var api = _app.MapGroup("/api").AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            var path = http.Request.Path.Value ?? string.Empty;

            if (path.Equals("/api/login", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("/api/status", StringComparison.OrdinalIgnoreCase))
            {
                return await next(context);
            }

            if (_settingsService.Settings.RemoteControl.RequireAuthentication)
            {
                if (!IsSessionValid(http))
                {
                    return Results.Unauthorized();
                }
            }

            try
            {
                return await next(context);
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { error = "Invalid JSON payload." });
            }
            catch (InvalidDataException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        });

        // ---------------------------------------------------------
        // Authentication
        // ---------------------------------------------------------
        api.MapPost("/login", async (HttpContext context) =>
        {
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            if (!_requestLimiter.AllowRequest(ip))
            {
                _auditLogService.Log(ip, "login", null, null, false, "Rate limit exceeded");
                return Results.StatusCode(StatusCodes.Status429TooManyRequests);
            }

            string? password = null;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync();
                password = form["password"].ToString();
            }
            else
            {
                var req = await ReadJsonAsync<RemoteLoginRequest>(context);
                password = req?.Password;
            }

            var settings = _settingsService.Settings.RemoteControl;
            if (settings.RequireAuthentication)
            {
                if (string.IsNullOrWhiteSpace(password))
                {
                    return Results.BadRequest(new { error = "Password is required" });
                }

                bool valid = !string.IsNullOrWhiteSpace(settings.PasswordHash) &&
                             _authService.VerifyPassword(password, settings.PasswordHash);

                if (!valid)
                {
                    _auditLogService.Log(ip, "login", null, null, false, "Invalid password");
                    return Results.BadRequest(new { error = "Invalid credentials" });
                }

                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTimeOffset.UtcNow.AddDays(1)
                };

                var stamp = settings.SecurityStamp;
                if (string.IsNullOrEmpty(stamp))
                {
                    stamp = Guid.NewGuid().ToString("N");
                    settings.SecurityStamp = stamp;
                    _settingsService.Save();
                }

                context.Response.Cookies.Append("PocketMCRemoteAuth", stamp, cookieOptions);
                _auditLogService.Log(ip, "login", null, null, true);
                return Results.Ok(new { success = true });
            }
            else
            {
                return Results.Ok(new { success = true });
            }
        });

        api.MapPost("/logout", (HttpContext context) =>
        {
            context.Response.Cookies.Delete("PocketMCRemoteAuth");
            return Results.Ok(new { success = true });
        });

        // ---------------------------------------------------------
        // Dashboard Status
        // ---------------------------------------------------------
        api.MapGet("/status", (HttpContext context) => Results.Ok(BuildDashboardStatus(context)));

        // ---------------------------------------------------------
        // Instances List & Status
        // ---------------------------------------------------------
        api.MapGet("/instances", async (HttpContext context) =>
        {
            var instances = await _statusService.GetInstancesAsync();
            return Results.Ok(instances);
        });

        api.MapGet("/instances/{instanceId}/status", async (HttpContext context, string instanceId) =>
        {
            var instance = await ResolveInstanceAsync(instanceId);
            if (instance == null) return Results.NotFound();

            var status = await _statusService.GetInstanceStatusAsync(instance.Id);
            return status != null ? Results.Ok(status) : Results.NotFound();
        });

        api.MapPost("/instances/batch", async (HttpContext context) =>
        {
            if (!_settingsService.Settings.RemoteControl.AllowRemotePlayerActions)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var request = await ReadJsonAsync<RemoteBatchActionRequest>(context);
            if (request == null) return Results.BadRequest(new { error = "Batch request payload is required." });

            string clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var results = await _instanceControlService.ExecuteBatchAsync(
                request.InstanceIds,
                request.Action,
                hasAccess: _ => true,
                audit: item => _auditLogService.Log(clientIp, $"batch.{request.Action}", item.InstanceId, null, item.Status == "success", item.Message),
                cancellationToken: context.RequestAborted);

            return Results.Ok(results);
        });

        api.MapPost("/instances/{instanceId}/start", async (HttpContext context, string instanceId) =>
        {
            if (!_settingsService.Settings.RemoteControl.AllowRemotePlayerActions) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var instance = await ResolveInstanceAsync(instanceId);
            if (instance == null) return Results.NotFound();

            var res = await _instanceControlService.StartAsync(instance.Id);
            _auditLogService.Log(context.Connection.RemoteIpAddress?.ToString(), "start", instance.Id, null, res.Success, res.Message);
            return ToActionResult(res);
        });

        api.MapPost("/instances/{instanceId}/stop", async (HttpContext context, string instanceId) =>
        {
            if (!_settingsService.Settings.RemoteControl.AllowRemotePlayerActions) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var instance = await ResolveInstanceAsync(instanceId);
            if (instance == null) return Results.NotFound();

            var res = await _instanceControlService.StopAsync(instance.Id);
            _auditLogService.Log(context.Connection.RemoteIpAddress?.ToString(), "stop", instance.Id, null, res.Success, res.Message);
            return ToActionResult(res);
        });

        api.MapPost("/instances/{instanceId}/restart", async (HttpContext context, string instanceId) =>
        {
            if (!_settingsService.Settings.RemoteControl.AllowRemotePlayerActions) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var instance = await ResolveInstanceAsync(instanceId);
            if (instance == null) return Results.NotFound();

            var res = await _instanceControlService.RestartAsync(instance.Id, context.RequestAborted);
            _auditLogService.Log(context.Connection.RemoteIpAddress?.ToString(), "restart", instance.Id, null, res.Success, res.Message);
            return ToActionResult(res);
        });

        // ---------------------------------------------------------
        // Console & Commands
        // ---------------------------------------------------------
        api.MapGet("/instances/{instanceId}/console/history", async (HttpContext context, string instanceId) =>
        {
            var instance = await ResolveInstanceAsync(instanceId);
            if (instance == null) return Results.NotFound();

            var history = _consoleLogService?.GetLogs(instance.Slug) ?? Array.Empty<string>();
            return Results.Ok(history);
        });

        api.MapPost("/instances/{instanceId}/console/command", async (HttpContext context, string instanceId) =>
        {
            if (!_settingsService.Settings.RemoteControl.AllowRemoteConsoleCommands) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var instance = await ResolveInstanceAsync(instanceId);
            if (instance == null) return Results.NotFound();

            string command = string.Empty;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync();
                command = form["command"].ToString();
            }
            else
            {
                var req = await ReadJsonAsync<RemoteCommandRequest>(context);
                command = req?.Command ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(command)) return Results.BadRequest(new { error = "Command is required." });

            bool isRunning = _processRunner.TryGetRunningInfo(instance.Slug, out _, out string state) && state == "Running";
            if (!isRunning) return Results.BadRequest(new { error = "Server instance is not running." });

            await _processRunner.SendCommandAsync(instance, command);
            _auditLogService.Log(context.Connection.RemoteIpAddress?.ToString(), "console.command", instance.Id, command);
            return Results.Ok(new { success = true });
        });

        // ---------------------------------------------------------
        // Player Actions
        // ---------------------------------------------------------
        api.MapGet("/instances/{instanceId}/players", async (HttpContext context, string instanceId) =>
        {
            var instance = await ResolveInstanceAsync(instanceId);
            if (instance == null) return Results.NotFound();

            var status = await _statusService.GetInstanceStatusAsync(instance.Id);
            var onlinePlayers = status?.OnlinePlayers ?? Array.Empty<RemotePlayerDto>();
            return Results.Ok(new { count = onlinePlayers.Count, players = onlinePlayers });
        });

        api.MapPost("/instances/{instanceId}/players/{name}/{action}", async (HttpContext context, string instanceId, string name, string action) =>
        {
            if (!_settingsService.Settings.RemoteControl.AllowRemotePlayerActions) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var instance = await ResolveInstanceAsync(instanceId);
            if (instance == null) return Results.NotFound();

            RemotePlayerActionRequest? req = null;
            if (context.Request.ContentLength > 0)
            {
                req = await ReadJsonAsync<RemotePlayerActionRequest>(context);
            }

            string clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var result = await _playerActionService.ExecuteAsync(instance.Id, name, action, req, clientIp);
            return ToActionResult(result);
        });

        // ---------------------------------------------------------
        // Properties & Settings
        // ---------------------------------------------------------
        api.MapGet("/instances/{instanceId}/properties", async (HttpContext context, string instanceId) =>
        {
            if (!_settingsService.Settings.RemoteControl.AllowRemoteServerSettings) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var instance = await ResolveInstanceAsync(instanceId);
            if (instance == null || !Directory.Exists(instance.Path)) return Results.NotFound();

            var dto = ServerPropertiesHelper.Load(instance.Path);
            return Results.Ok(dto);
        });

        api.MapPut("/instances/{instanceId}/properties", async (HttpContext context, string instanceId) =>
        {
            if (!_settingsService.Settings.RemoteControl.AllowRemoteServerSettings) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var instance = await ResolveInstanceAsync(instanceId);
            if (instance == null || !Directory.Exists(instance.Path)) return Results.NotFound();

            var request = await ReadJsonAsync<RemoteServerPropertiesDto>(context);
            if (request == null || !request.IsValid()) return Results.BadRequest(new { error = "Invalid properties payload or unsupported values." });

            using var operation = await _operations.AcquireAsync(instance.Path, cancellationToken: context.RequestAborted);
            ServerPropertiesHelper.Save(instance.Path, request);
            _auditLogService.Log(context.Connection.RemoteIpAddress?.ToString(), "properties.update", instance.Id);
            return Results.Ok(new { success = true });
        });

        api.MapGet("/instances/{instanceId}/reboot-schedule", async (HttpContext context, string instanceId) =>
        {
            if (!_settingsService.Settings.RemoteControl.AllowRemoteServerSettings) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var instance = await ResolveInstanceAsync(instanceId);
            if (instance == null) return Results.NotFound();

            return Results.Ok(new
            {
                schedule = new { Enabled = false, CronExpression = "", WarningMinutes = 5 },
                hostTimeZone = TimeZoneInfo.Local.DisplayName
            });
        });

        api.MapPut("/instances/{instanceId}/reboot-schedule", async (HttpContext context, string instanceId) =>
        {
            if (!_settingsService.Settings.RemoteControl.AllowRemoteServerSettings) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var instance = await ResolveInstanceAsync(instanceId);
            if (instance == null) return Results.NotFound();

            return Results.Ok(new { success = true });
        });

        // ---------------------------------------------------------
        // Addons Management
        // ---------------------------------------------------------
        api.MapGet("/instances/{instanceId}/addons", async (HttpContext context, string instanceId) =>
        {
            if (!_settingsService.Settings.RemoteControl.AllowRemoteServerAddons) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var instance = await ResolveInstanceAsync(instanceId);
            if (instance == null || !Directory.Exists(instance.Path)) return Results.NotFound();

            var addons = GetInstalledAddonsForInstance(instance.Path);
            return Results.Ok(addons);
        });

        api.MapPost("/instances/{instanceId}/addons/uninstall", async (HttpContext context, string instanceId) =>
        {
            if (!_settingsService.Settings.RemoteControl.AllowRemoteServerAddons) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var instance = await ResolveInstanceAsync(instanceId);
            if (instance == null || !Directory.Exists(instance.Path)) return Results.NotFound();

            var req = await ReadJsonAsync<RemoteUninstallAddonRequest>(context);
            if (string.IsNullOrWhiteSpace(req?.AddonPathOrId)) return Results.BadRequest(new { error = "Addon path is required." });

            string? targetPath = ResolveInstalledAddon(instance.Path, req.AddonPathOrId);
            if (targetPath == null) return Results.BadRequest(new { error = "Path is outside instance folder." });

            bool isRunning = _processRunner.TryGetRunningInfo(instance.Slug, out _, out string state) && state == "Running";
            if (isRunning) return Results.Conflict(new { error = "Stop the server before removing add-ons." });

            using var operation = await _operations.AcquireAsync(instance.Path, requiresStopped: true, cancellationToken: context.RequestAborted);
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
                _auditLogService.Log(context.Connection.RemoteIpAddress?.ToString(), "addon.uninstall", instance.Id, req.AddonPathOrId);
                return Results.Ok(new { success = true });
            }
            if (Directory.Exists(targetPath))
            {
                Directory.Delete(targetPath, recursive: true);
                _auditLogService.Log(context.Connection.RemoteIpAddress?.ToString(), "addon.uninstall", instance.Id, req.AddonPathOrId);
                return Results.Ok(new { success = true });
            }

            return Results.NotFound(new { error = "Addon target not found." });
        });

        // ---------------------------------------------------------
        // File Manager
        // ---------------------------------------------------------
        api.MapGet("/instances/{instanceId}/files", async (HttpContext context, string instanceId, string? path) =>
        {
            if (!_settingsService.Settings.RemoteControl.AllowRemoteFileManager) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var instance = await ResolveInstanceAsync(instanceId);
            if (instance == null || !Directory.Exists(instance.Path)) return Results.NotFound(new { error = "Instance folder not found." });

            if (!CanAccessRemoteFile(context, path ?? string.Empty)) return Results.StatusCode(StatusCodes.Status403Forbidden);

            string? targetPath = GetSanitizedPath(instance.Path, path ?? string.Empty);
            if (targetPath == null || (!Directory.Exists(targetPath) && !File.Exists(targetPath)))
            {
                return Results.BadRequest(new { error = "Invalid directory or file path." });
            }

            if (File.Exists(targetPath))
            {
                var fi = new FileInfo(targetPath);
                return Results.Ok(new List<RemoteFileItemDto>
                {
                    new RemoteFileItemDto
                    {
                        Name = fi.Name,
                        RelativePath = Path.GetRelativePath(instance.Path, fi.FullName).Replace('\\', '/'),
                        IsDirectory = false,
                        SizeBytes = fi.Length,
                        LastModified = fi.LastWriteTimeUtc,
                        Extension = fi.Extension.ToLowerInvariant()
                    }
                });
            }

            var items = new List<RemoteFileItemDto>();
            var dirInfo = new DirectoryInfo(targetPath);

            foreach (var dir in dirInfo.GetDirectories())
            {
                if (!CanAccessRemoteFile(context, Path.GetRelativePath(instance.Path, dir.FullName))) continue;
                if (dir.Name.StartsWith(".") && dir.Name != ".pocket-mc.json") continue;

                items.Add(new RemoteFileItemDto
                {
                    Name = dir.Name,
                    RelativePath = Path.GetRelativePath(instance.Path, dir.FullName).Replace('\\', '/'),
                    IsDirectory = true,
                    SizeBytes = 0,
                    LastModified = dir.LastWriteTimeUtc,
                    Extension = string.Empty
                });
            }

            foreach (var file in dirInfo.GetFiles())
            {
                if (!CanAccessRemoteFile(context, Path.GetRelativePath(instance.Path, file.FullName))) continue;

                items.Add(new RemoteFileItemDto
                {
                    Name = file.Name,
                    RelativePath = Path.GetRelativePath(instance.Path, file.FullName).Replace('\\', '/'),
                    IsDirectory = false,
                    SizeBytes = file.Length,
                    LastModified = file.LastWriteTimeUtc,
                    Extension = file.Extension.ToLowerInvariant()
                });
            }

            return Results.Ok(items.OrderByDescending(i => i.IsDirectory).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase));
        });

        api.MapGet("/instances/{instanceId}/files/content", async (HttpContext context, string instanceId, string? path) =>
        {
            if (!_settingsService.Settings.RemoteControl.AllowRemoteFileManager) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var instance = await ResolveInstanceAsync(instanceId);
            if (instance == null || !Directory.Exists(instance.Path)) return Results.NotFound(new { error = "Instance folder not found." });

            if (!CanAccessRemoteFile(context, path ?? string.Empty)) return Results.StatusCode(StatusCodes.Status403Forbidden);

            string? targetPath = GetSanitizedPath(instance.Path, path ?? string.Empty);
            if (targetPath == null || !File.Exists(targetPath)) return Results.BadRequest(new { error = "File not found." });

            var fi = new FileInfo(targetPath);
            if (fi.Length > 1 * 1024 * 1024)
            {
                return Results.Ok(new RemoteFileContentDto
                {
                    RelativePath = Path.GetRelativePath(instance.Path, fi.FullName).Replace('\\', '/'),
                    Content = "[File exceeds 1 MB limit for browser editing]",
                    IsText = true,
                    IsTruncated = true,
                    SizeBytes = fi.Length
                });
            }

            if (RemoteTextFileContent.IsKnownBinaryPath(targetPath))
            {
                return Results.Ok(new RemoteFileContentDto
                {
                    RelativePath = Path.GetRelativePath(instance.Path, fi.FullName).Replace('\\', '/'),
                    Content = "[Binary file cannot be viewed in text editor]",
                    IsText = false,
                    SizeBytes = fi.Length
                });
            }

            return Results.Ok(RemoteTextFileContent.Read(targetPath, Path.GetRelativePath(instance.Path, fi.FullName).Replace('\\', '/')));
        });

        api.MapPut("/instances/{instanceId}/files/content", async (HttpContext context, string instanceId) =>
        {
            if (!_settingsService.Settings.RemoteControl.AllowRemoteFileManager) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var instance = await ResolveInstanceAsync(instanceId);
            if (instance == null || !Directory.Exists(instance.Path)) return Results.NotFound(new { error = "Instance folder not found." });

            var req = await ReadJsonAsync<SaveRemoteFileContentRequest>(context);
            if (req == null || string.IsNullOrWhiteSpace(req.RelativePath) || req.Content?.Length > 1_048_576)
            {
                return Results.BadRequest(new { error = "RelativePath and valid content are required." });
            }

            if (!CanAccessRemoteFile(context, req.RelativePath)) return Results.StatusCode(StatusCodes.Status403Forbidden);

            string? targetPath = GetSanitizedPath(instance.Path, req.RelativePath);
            if (targetPath == null) return Results.BadRequest(new { error = "Invalid file path." });

            using var operation = await _operations.AcquireAsync(instance.Path, cancellationToken: context.RequestAborted);
            if (RemoteTextFileContent.IsKnownBinaryPath(targetPath) ||
                !RemoteTextFileContent.IsPlainText(req.Content ?? string.Empty) ||
                (File.Exists(targetPath) && !RemoteTextFileContent.Read(targetPath, req.RelativePath).IsText))
            {
                return Results.BadRequest(new { error = "The browser editor supports UTF-8 text files up to 1 MB. Binary files cannot be overwritten here." });
            }

            string tempPath = targetPath + $".tmp.{Guid.NewGuid():N}";
            await File.WriteAllTextAsync(tempPath, req.Content ?? string.Empty, System.Text.Encoding.UTF8, context.RequestAborted);
            File.Move(tempPath, targetPath, overwrite: true);

            _auditLogService.Log(context.Connection.RemoteIpAddress?.ToString(), "file.save", instance.Id, req.RelativePath);
            return Results.Ok(new { success = true });
        });

        api.MapDelete("/instances/{instanceId}/files", async (HttpContext context, string instanceId, string? path) =>
        {
            if (!_settingsService.Settings.RemoteControl.AllowRemoteFileManager) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var instance = await ResolveInstanceAsync(instanceId);
            if (instance == null || !Directory.Exists(instance.Path)) return Results.NotFound(new { error = "Instance folder not found." });

            if (string.IsNullOrWhiteSpace(path)) return Results.BadRequest(new { error = "Path is required." });
            if (!CanAccessRemoteFile(context, path)) return Results.StatusCode(StatusCodes.Status403Forbidden);

            using var operation = await _operations.AcquireAsync(instance.Path, cancellationToken: context.RequestAborted);

            string? targetPath = GetSanitizedPath(instance.Path, path);
            if (targetPath == null || string.Equals(targetPath, Path.GetFullPath(instance.Path), StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { error = "Cannot delete root instance folder." });
            }

            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
                _auditLogService.Log(context.Connection.RemoteIpAddress?.ToString(), "file.delete", instance.Id, path);
                return Results.Ok(new { success = true });
            }
            if (Directory.Exists(targetPath))
            {
                Directory.Delete(targetPath, recursive: true);
                _auditLogService.Log(context.Connection.RemoteIpAddress?.ToString(), "file.delete_dir", instance.Id, path);
                return Results.Ok(new { success = true });
            }

            return Results.NotFound(new { error = "File or folder not found." });
        });

        api.MapPost("/instances/{instanceId}/files/upload", async (HttpContext context, string instanceId, string? path) =>
        {
            if (!_settingsService.Settings.RemoteControl.AllowRemoteFileManager) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var instance = await ResolveInstanceAsync(instanceId);
            if (instance == null || !Directory.Exists(instance.Path)) return Results.NotFound(new { error = "Instance folder not found." });

            if (!context.Request.HasFormContentType) return Results.BadRequest(new { error = "Form-data with files is required." });

            var form = await context.Request.ReadFormAsync();
            string targetFolderRel = form.TryGetValue("path", out var formPath) ? formPath.ToString() : (path ?? string.Empty);
            if (!CanAccessRemoteFile(context, targetFolderRel) || form.Files.Any(file =>
                !CanAccessRemoteFile(context, Path.Combine(targetFolderRel, Path.GetFileName(file.FileName)))))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            string? targetDirectory = GetSanitizedPath(instance.Path, targetFolderRel);
            if (targetDirectory == null) return Results.BadRequest(new { error = "Invalid upload directory path." });

            if (!Directory.Exists(targetDirectory)) Directory.CreateDirectory(targetDirectory);

            int uploadedCount = 0;
            foreach (var file in form.Files)
            {
                if (file.Length <= 0) continue;
                string fileName = Path.GetFileName(file.FileName);
                if (string.IsNullOrWhiteSpace(fileName)) continue;

                string? destinationPath = GetSanitizedPath(instance.Path, Path.Combine(targetFolderRel, fileName));
                if (destinationPath == null) continue;

                using var operation = await _operations.AcquireAsync(instance.Path, cancellationToken: context.RequestAborted);
                string staged = destinationPath + $".{Guid.NewGuid():N}.upload";
                try
                {
                    await using (var targetStream = new FileStream(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        await file.CopyToAsync(targetStream, context.RequestAborted);
                    }
                    File.Move(staged, destinationPath, overwrite: true);
                    uploadedCount++;
                    _auditLogService.Log(context.Connection.RemoteIpAddress?.ToString(), "file.upload", instance.Id, Path.GetRelativePath(instance.Path, destinationPath));
                }
                finally
                {
                    if (File.Exists(staged)) File.Delete(staged);
                }
            }

            return Results.Ok(new { success = true, uploaded = uploadedCount });
        });

        // ---------------------------------------------------------
        // Backups
        // ---------------------------------------------------------
        api.MapGet("/instances/{instanceId}/backups", async (HttpContext context, string instanceId) =>
        {
            if (!_settingsService.Settings.RemoteControl.AllowRemoteServerBackups) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var instance = await ResolveInstanceAsync(instanceId);
            if (instance == null || !Directory.Exists(instance.Path)) return Results.NotFound(new { error = "Instance folder not found." });

            var list = new List<RemoteBackupDto>();
            var defaultDir = Path.Combine(instance.Path, "backups");
            var customDir = instance.CustomBackupDirectory;

            var directoriesToScan = new List<string> { defaultDir };
            if (!string.IsNullOrWhiteSpace(customDir) && customDir != defaultDir && Directory.Exists(customDir))
            {
                directoriesToScan.Add(customDir);
            }

            var zipFiles = new List<string>();
            foreach (var dir in directoriesToScan)
            {
                if (Directory.Exists(dir))
                {
                    try { zipFiles.AddRange(Directory.GetFiles(dir, "*.zip")); } catch { }
                }
            }

            var uniqueZips = zipFiles
                .Select(f => new FileInfo(f))
                .GroupBy(fi => fi.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderByDescending(fi => fi.CreationTime)
                .ToList();

            bool isRunning = _activeBackups.TryGetValue(instance.Id, out bool r) && r;

            foreach (var fi in uniqueZips)
            {
                list.Add(new RemoteBackupDto
                {
                    Id = fi.Name,
                    FileName = fi.Name,
                    SizeBytes = fi.Length,
                    CreatedAt = fi.CreationTime,
                    Type = fi.FullName.StartsWith(defaultDir, StringComparison.OrdinalIgnoreCase) ? "Local" : "Custom",
                    IsAutomated = fi.Name.Contains("auto", StringComparison.OrdinalIgnoreCase),
                    ServerVersion = instance.EngineVersion,
                    ServerType = instance.EngineType.ToString()
                });
            }

            return Results.Ok(new { isBackupRunning = isRunning, backups = list });
        });

        api.MapPost("/instances/{instanceId}/backups", async (HttpContext context, string instanceId) =>
        {
            if (!_settingsService.Settings.RemoteControl.AllowRemoteServerBackups) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var instance = await ResolveInstanceAsync(instanceId);
            if (instance == null || !Directory.Exists(instance.Path)) return Results.NotFound(new { error = "Instance folder not found." });

            if (_backupService == null) return Results.BadRequest(new { error = "Backup service is unavailable." });

            if (!_activeBackups.TryAdd(instance.Id, true))
            {
                return Results.Conflict(new { error = "A backup is already running for this instance." });
            }

            _auditLogService.Log(context.Connection.RemoteIpAddress?.ToString(), "backup.create", instance.Id, "Manual Backup");

            _ = Task.Run(async () =>
            {
                try
                {
                    await _backupService.CreateBackupAsync(instance, BackupTrigger.Manual, "Remote Control Backup");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Background backup failed for instance {InstanceId}", instance.Id);
                }
                finally
                {
                    _activeBackups.TryRemove(instance.Id, out _);
                }
            });

            return Results.Ok(new { success = true, fileName = "Started in background" });
        });

        // ---------------------------------------------------------
        // WebSockets
        // ---------------------------------------------------------
        _app.Map("/ws/instances/{instanceId}/console", async (HttpContext context, string instanceId) =>
        {
            if (_settingsService.Settings.RemoteControl.RequireAuthentication && !IsSessionValid(context))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            if (!_settingsService.Settings.RemoteControl.AllowRemoteConsoleCommands)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            var instance = await ResolveInstanceAsync(instanceId);
            if (instance == null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            if (context.WebSockets.IsWebSocketRequest)
            {
                using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                await _wsHandler.HandleWebSocketAsync(context, webSocket, instance.Slug);
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        });

        await _app.StartAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _startGate.WaitAsync(cancellationToken);
        try
        {
            await StopInternalAsync(cancellationToken);
        }
        finally
        {
            _startGate.Release();
        }
    }

    private async Task StopInternalAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _tunnelManager.StopAllTunnelsAsync();
        }
        catch { }

        if (_app != null)
        {
            await _app.StopAsync(cancellationToken);
            await _app.DisposeAsync();
            _app = null;
        }
    }

    private async Task<ServerInstance?> ResolveInstanceAsync(string instanceIdOrSlug)
    {
        var all = await _instanceService.ListInstancesAsync();
        if (Guid.TryParse(instanceIdOrSlug, out var guid))
        {
            var byId = all.FirstOrDefault(i => i.Id == guid);
            if (byId != null) return byId;
        }

        return all.FirstOrDefault(i => string.Equals(i.Slug, instanceIdOrSlug, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsSessionValid(HttpContext context)
    {
        var settings = _settingsService.Settings.RemoteControl;
        if (!settings.RequireAuthentication) return true;

        if (!context.Request.Cookies.TryGetValue("PocketMCRemoteAuth", out var sessionValue) ||
            string.IsNullOrWhiteSpace(sessionValue))
        {
            return false;
        }

        return string.Equals(sessionValue, settings.SecurityStamp, StringComparison.Ordinal);
    }

    private RemoteDashboardStatus BuildDashboardStatus(HttpContext context)
    {
        var settings = _settingsService.Settings.RemoteControl;

        return new RemoteDashboardStatus
        {
            Enabled = settings.Enabled,
            HostRunning = IsRunning,
            Port = settings.Port,
            AccessMode = settings.AccessMode,
            LocalUrls = _localNetworkAddressService.GetLocalUrls(settings.Port),
            PublicUrl = null,
            TunnelRunning = false,
            TunnelError = null,
            AllowRemoteConsoleCommands = settings.AllowRemoteConsoleCommands,
            AllowRemotePlayerActions = settings.AllowRemotePlayerActions,
            AllowRemoteServerSettings = settings.AllowRemoteServerSettings,
            AllowRemoteServerAddons = settings.AllowRemoteServerAddons,
            AllowRemoteFileManager = settings.AllowRemoteFileManager,
            AllowRemoteServerBackups = settings.AllowRemoteServerBackups
        };
    }

    private bool CanAccessRemoteFile(HttpContext context, string relativePath)
    {
        return RemoteFileAccessPolicy.CanAccess(relativePath, authenticatedAdmin: true);
    }

    private static string? GetSanitizedPath(string baseDir, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(baseDir) || !Directory.Exists(baseDir) || PathSafety.HasReparsePoint(baseDir)) return null;

        var normalizedRel = (relativePath ?? string.Empty).TrimStart('/', '\\');
        if (string.IsNullOrWhiteSpace(normalizedRel))
        {
            return Path.GetFullPath(baseDir);
        }

        return PathSafety.ValidateContainedPath(baseDir, normalizedRel);
    }

    private static string? ResolveInstalledAddon(string serverDir, string relativePath)
    {
        string? target = PathSafety.ValidateContainedPath(serverDir, relativePath);
        if (target == null) return null;
        string normalized = Path.GetRelativePath(serverDir, target).Replace('\\', '/');
        return GetInstalledAddonsForInstance(serverDir).Any(a => string.Equals(a.FilePath, normalized, StringComparison.OrdinalIgnoreCase)) ? target : null;
    }

    private static List<RemoteAddonDto> GetInstalledAddonsForInstance(string serverDir)
    {
        var result = new List<RemoteAddonDto>();
        string[] subdirs = new[] { "plugins", "mods", "behavior_packs", "resource_packs" };

        foreach (var dirName in subdirs)
        {
            string dirPath = Path.Combine(serverDir, dirName);
            if (!Directory.Exists(dirPath)) continue;

            var dirInfo = new DirectoryInfo(dirPath);
            foreach (var file in dirInfo.GetFiles("*.*", SearchOption.TopDirectoryOnly))
            {
                string ext = file.Extension.ToLowerInvariant();
                if (ext == ".jar" || ext == ".phar" || ext == ".mcpack" || ext == ".zip")
                {
                    string relPath = Path.Combine(dirName, file.Name).Replace('\\', '/');
                    result.Add(new RemoteAddonDto
                    {
                        Name = file.Name,
                        FilePath = relPath,
                        SizeKb = Math.Round(file.Length / 1024.0, 1),
                        LastModified = file.LastWriteTimeUtc.ToString("o"),
                        AddonType = dirName == "plugins" ? "plugin" : dirName == "mods" ? "mod" : "pack"
                    });
                }
            }

            foreach (var subDir in dirInfo.GetDirectories())
            {
                string relPath = Path.Combine(dirName, subDir.Name).Replace('\\', '/');
                result.Add(new RemoteAddonDto
                {
                    Name = subDir.Name,
                    FilePath = relPath,
                    SizeKb = 0,
                    LastModified = subDir.LastWriteTimeUtc.ToString("o"),
                    AddonType = dirName == "plugins" ? "plugin" : dirName == "mods" ? "mod" : "pack"
                });
            }
        }

        return result;
    }

    private static async Task<T?> ReadJsonAsync<T>(HttpContext context)
    {
        if (context.Request.ContentLength == 0) return default;
        int limit = context.Request.Path.Value?.EndsWith("/files/content", StringComparison.Ordinal) == true ? 1_100_000 : 32_768;
        if (context.Request.ContentLength > limit) throw new InvalidDataException("JSON payload exceeds allowed limit.");

        using var buffer = new MemoryStream();
        byte[] chunk = new byte[4096];
        int read;
        while ((read = await context.Request.Body.ReadAsync(chunk, context.RequestAborted)) > 0)
        {
            if (buffer.Length + read > limit) throw new InvalidDataException("JSON payload exceeds allowed limit.");
            buffer.Write(chunk, 0, read);
        }
        if (buffer.Length == 0) return default;
        return JsonSerializer.Deserialize<T>(buffer.ToArray(), JsonOptions);
    }

    private static IResult ToActionResult(RemoteControlActionResult result)
    {
        if (result.Success) return Results.Ok(new { success = true });

        return result.Failure switch
        {
            RemoteControlActionFailure.NotFound => Results.NotFound(new { error = result.Message }),
            RemoteControlActionFailure.NotRunning => Results.BadRequest(new { error = result.Message }),
            RemoteControlActionFailure.Disabled => Results.StatusCode(StatusCodes.Status403Forbidden),
            _ => Results.BadRequest(new { error = result.Message })
        };
    }
}
