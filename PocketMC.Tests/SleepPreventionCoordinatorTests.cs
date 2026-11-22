using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Power;
using Xunit;

namespace PocketMC.Tests;

public class SleepPreventionCoordinatorTests
{
    private class FakeSleepPreventionService : ISleepPreventionService
    {
        public bool IsPreventingSleep { get; private set; }
        public int PreventCalls { get; private set; }
        public int AllowCalls { get; private set; }

        public void PreventSleep()
        {
            IsPreventingSleep = true;
            PreventCalls++;
        }

        public void AllowSleep()
        {
            IsPreventingSleep = false;
            AllowCalls++;
        }

        public void Dispose()
        {
            AllowSleep();
        }
    }

    [Fact]
    public void Refresh_WhenServerRunningAndSettingEnabled_CallsPreventSleep()
    {
        var fakeSleep = new FakeSleepPreventionService();
        var settings = new Settings
        {
            KeepComputerAwakeWhileServersRunning = true
        };

        var coordinator = new ServerSleepPreventionCoordinator(
            fakeSleep,
            () => settings,
            () => 1, // 1 running server
            NullLogger<ServerSleepPreventionCoordinator>.Instance);

        coordinator.Refresh();

        Assert.True(fakeSleep.IsPreventingSleep);
        Assert.Equal(1, fakeSleep.PreventCalls);
    }

    [Fact]
    public void Refresh_WhenNoServerRunning_CallsAllowSleep()
    {
        var fakeSleep = new FakeSleepPreventionService();
        var settings = new Settings { KeepComputerAwakeWhileServersRunning = true };

        var coordinator = new ServerSleepPreventionCoordinator(
            fakeSleep,
            () => settings,
            () => 0, // 0 running servers
            NullLogger<ServerSleepPreventionCoordinator>.Instance);

        coordinator.Refresh();

        Assert.False(fakeSleep.IsPreventingSleep);
        Assert.Equal(1, fakeSleep.AllowCalls);
    }

    [Fact]
    public void Refresh_WhenSettingDisabled_CallsAllowSleepEvenIfServersRunning()
    {
        var fakeSleep = new FakeSleepPreventionService();
        var settings = new Settings { KeepComputerAwakeWhileServersRunning = false };

        var coordinator = new ServerSleepPreventionCoordinator(
            fakeSleep,
            () => settings,
            () => 2, // 2 servers running
            NullLogger<ServerSleepPreventionCoordinator>.Instance);

        coordinator.Refresh();

        Assert.False(fakeSleep.IsPreventingSleep);
        Assert.Equal(1, fakeSleep.AllowCalls);
    }

    [Fact]
    public void Dispose_ReleasesSleepPrevention()
    {
        var fakeSleep = new FakeSleepPreventionService();
        var settings = new Settings { KeepComputerAwakeWhileServersRunning = true };

        var coordinator = new ServerSleepPreventionCoordinator(
            fakeSleep,
            () => settings,
            () => 1,
            NullLogger<ServerSleepPreventionCoordinator>.Instance);

        coordinator.Refresh();
        Assert.True(fakeSleep.IsPreventingSleep);

        coordinator.Dispose();
        Assert.False(fakeSleep.IsPreventingSleep);
    }
}
