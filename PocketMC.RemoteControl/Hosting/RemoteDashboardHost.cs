using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using PocketMC.Core.Services;
using PocketMC.RemoteControl.Services;

using PocketMC.RemoteControl.Tunnels;

namespace PocketMC.RemoteControl.Hosting;

public class RemoteDashboardHost : IHostedService
{
    private readonly ISettingsService _settingsService;
    private readonly IInstanceService _instanceService;
    private readonly IProcessRunner _processRunner;
    private readonly RemoteAuthenticationService _authService;
    private readonly RemoteAuditLogService _auditLogService;
    private readonly RemoteRequestLimiter _requestLimiter;
    private readonly RemoteConsoleWebSocketHandler _wsHandler;
    private readonly RemoteTunnelManager _tunnelManager;

    private WebApplication? _app;

    public RemoteDashboardHost(
        ISettingsService settingsService,
        IInstanceService instanceService,
        IProcessRunner processRunner,
        RemoteAuthenticationService authService,
        RemoteAuditLogService auditLogService,
        RemoteRequestLimiter requestLimiter,
        RemoteConsoleWebSocketHandler wsHandler,
        RemoteTunnelManager tunnelManager)
    {
        _settingsService = settingsService;
        _instanceService = instanceService;
        _processRunner = processRunner;
        _authService = authService;
        _auditLogService = auditLogService;
        _requestLimiter = requestLimiter;
        _wsHandler = wsHandler;
        _tunnelManager = tunnelManager;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_settingsService.Settings.RemoteControl.Enabled) return;
        await StartServerInternalAsync(_settingsService.Settings.RemoteControl.Port, cancellationToken);
    }

    public async Task StartServerAsync(int port)
    {
        await StopAsync(CancellationToken.None);
        await StartServerInternalAsync(port, CancellationToken.None);
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
                    var path = ctx.Context.Request.Path.Value ?? "";
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

        _app.Use(async (context, next) =>
        {
            var isStatic = context.Request.Path == "/" || context.Request.Path.Value?.EndsWith(".js") == true || context.Request.Path.Value?.EndsWith(".css") == true;
            if (isStatic || context.Request.Path == "/api/login")
            {
                await next();
                return;
            }

            if (_settingsService.Settings.RemoteControl.RequireAuthentication)
            {
                if (!context.Request.Cookies.TryGetValue("PocketMCRemoteAuth", out var sessionValue) || 
                    sessionValue != _settingsService.Settings.RemoteControl.SecurityStamp)
                {
                    context.Response.StatusCode = 401;
                    return;
                }
            }
            await next();
        });

        _app.MapPost("/api/login", async (HttpContext context) =>
        {
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            if (!_requestLimiter.AllowRequest(ip))
            {
                _auditLogService.LogAction(ip, "login", "", false, "Rate limit exceeded");
                return Results.StatusCode(429);
            }

            var form = await context.Request.ReadFormAsync();
            var password = form["password"].ToString();
            var hash = _settingsService.Settings.RemoteControl.PasswordHash;

            if (hash != null && _authService.VerifyPassword(password, hash))
            {
                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTimeOffset.UtcNow.AddDays(1)
                };
                
                var stamp = _settingsService.Settings.RemoteControl.SecurityStamp;
                if (string.IsNullOrEmpty(stamp))
                {
                    stamp = Guid.NewGuid().ToString("N");
                    _settingsService.Settings.RemoteControl.SecurityStamp = stamp;
                    _settingsService.Save();
                }

                context.Response.Cookies.Append("PocketMCRemoteAuth", stamp, cookieOptions);
                _auditLogService.LogAction(ip, "login", "", true);
                return Results.Ok();
            }

            _auditLogService.LogAction(ip, "login", "", false, "Invalid password");
            return Results.Unauthorized();
        });

        _app.MapGet("/api/status", () => Results.Ok(new { Enabled = true }));

        _app.MapGet("/api/instances", async () =>
        {
            var allInstances = await _instanceService.ListInstancesAsync();
            var instances = allInstances.Select(i => new
            {
                i.Slug,
                i.Name,
                i.EngineType,
                i.EngineVersion,
                IsRunning = _processRunner.TryGetRunningInfo(i.Slug, out _, out _)
            });
            return Results.Ok(instances);
        });

        _app.MapPost("/api/instances/{id}/start", async (string id, HttpContext context) =>
        {
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            if (!_settingsService.Settings.RemoteControl.AllowRemotePlayerActions)
            {
                _auditLogService.LogAction(ip, "start", id, false, "Actions disabled by settings");
                return Results.Forbid();
            }

            var allInstances = await _instanceService.ListInstancesAsync();
            var instance = allInstances.FirstOrDefault(i => i.Slug == id);
            if (instance != null)
            {
                await _processRunner.StartAsync(instance);
                _auditLogService.LogAction(ip, "start", id, true);
                return Results.Ok();
            }
            return Results.NotFound();
        });

        _app.MapPost("/api/instances/{id}/stop", async (string id, HttpContext context) =>
        {
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            if (!_settingsService.Settings.RemoteControl.AllowRemotePlayerActions)
            {
                _auditLogService.LogAction(ip, "stop", id, false, "Actions disabled by settings");
                return Results.Forbid();
            }

            var allInstances = await _instanceService.ListInstancesAsync();
            var instance = allInstances.FirstOrDefault(i => i.Slug == id);
            if (instance != null)
            {
                await _processRunner.StopAsync(instance);
                _auditLogService.LogAction(ip, "stop", id, true);
                return Results.Ok();
            }
            return Results.NotFound();
        });

        _app.MapPost("/api/instances/{id}/restart", async (string id, HttpContext context) =>
        {
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            if (!_settingsService.Settings.RemoteControl.AllowRemotePlayerActions)
            {
                _auditLogService.LogAction(ip, "restart", id, false, "Actions disabled by settings");
                return Results.Forbid();
            }

            var allInstances = await _instanceService.ListInstancesAsync();
            var instance = allInstances.FirstOrDefault(i => i.Slug == id);
            if (instance != null)
            {
                await _processRunner.StopAsync(instance);
                await Task.Delay(1000);
                await _processRunner.StartAsync(instance);
                _auditLogService.LogAction(ip, "restart", id, true);
                return Results.Ok();
            }
            return Results.NotFound();
        });

        _app.MapPost("/api/instances/{id}/console/command", async (string id, HttpContext context) =>
        {
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            if (!_settingsService.Settings.RemoteControl.AllowRemoteConsoleCommands)
            {
                _auditLogService.LogAction(ip, "command", id, false, "Commands disabled by settings");
                return Results.Forbid();
            }

            var form = await context.Request.ReadFormAsync();
            var command = form["command"].ToString();

            var allInstances = await _instanceService.ListInstancesAsync();
            var instance = allInstances.FirstOrDefault(i => i.Slug == id);
            if (instance != null)
            {
                await _processRunner.SendCommandAsync(instance, command);
                _auditLogService.LogAction(ip, "command", id, true);
                return Results.Ok();
            }
            return Results.NotFound();
        });

        _app.MapGet("/ws/instances/{id}/console", async (string id, HttpContext context) =>
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                await _wsHandler.HandleWebSocketAsync(context, webSocket, id);
            }
            else
            {
                context.Response.StatusCode = 400;
            }
        });

        await _app.StartAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _tunnelManager.StopAllTunnelsAsync();
        }
        catch {}

        if (_app != null)
        {
            await _app.StopAsync(cancellationToken);
            await _app.DisposeAsync();
            _app = null;
        }
    }
}
