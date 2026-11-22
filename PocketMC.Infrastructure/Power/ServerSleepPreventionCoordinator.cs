using System;
using Microsoft.Extensions.Logging;
using PocketMC.Core.Models;
using PocketMC.Core.Services;

namespace PocketMC.Infrastructure.Power;

public sealed class ServerSleepPreventionCoordinator : IDisposable
{
    private readonly ISleepPreventionService _sleepPreventionService;
    private readonly Func<Settings> _settingsProvider;
    private readonly Func<int> _activeServerCountProvider;
    private readonly ILogger<ServerSleepPreventionCoordinator> _logger;
    private readonly IProcessRunner? _processRunner;
    private readonly object _gate = new();
    private bool _disposed;

    public ServerSleepPreventionCoordinator(
        ISleepPreventionService sleepPreventionService,
        Func<Settings> settingsProvider,
        Func<int> activeServerCountProvider,
        ILogger<ServerSleepPreventionCoordinator> logger,
        IProcessRunner? processRunner = null)
    {
        _sleepPreventionService = sleepPreventionService;
        _settingsProvider = settingsProvider;
        _activeServerCountProvider = activeServerCountProvider;
        _logger = logger;
        _processRunner = processRunner;

        if (_processRunner != null)
        {
            _processRunner.StateChanged += OnProcessStateChanged;
        }
    }

    private void OnProcessStateChanged(string instanceSlug, string state)
    {
        Refresh();
    }

    public void Refresh()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                var settings = _settingsProvider();
                int runningCount = _activeServerCountProvider();

                bool shouldPreventSleep = settings.KeepComputerAwakeWhileServersRunning && runningCount > 0;

                if (shouldPreventSleep)
                {
                    _sleepPreventionService.PreventSleep();
                }
                else
                {
                    _sleepPreventionService.AllowSleep();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh server sleep prevention state.");
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_processRunner != null)
            {
                _processRunner.StateChanged -= OnProcessStateChanged;
            }

            _sleepPreventionService.AllowSleep();
        }
    }
}
