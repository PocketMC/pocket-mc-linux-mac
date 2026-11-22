using System;
using System.Collections.Generic;
using PocketMC.App.ViewModels;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.RemoteControl.Services;
using Xunit;

namespace PocketMC.Tests;

public class RemoteControlViewModelTests
{
    private class FakeSettingsService : ISettingsService
    {
        public Settings Settings { get; set; } = new();
        public void Load() { }
        public void Save() { }
        public string GetSettingsDirectory() => "/tmp/pocketmc";
        public string GetDataRoot() => "/tmp/pocketmc/data";
        public string GetDefaultDataRoot() => "/tmp/pocketmc/data";
        public void SetDataRoot(string path) { }
        public string GetInstancesDirectory() => "/tmp/pocketmc/data/instances";
        public string GetBackupsDirectory() => "/tmp/pocketmc/data/backups";
        public string GetDownloadsDirectory() => "/tmp/pocketmc/downloads";
        public string GetCacheDirectory() => "/tmp/pocketmc/cache";
        public string GetLogsDirectory() => "/tmp/pocketmc/logs";
    }

    [Fact]
    public void RemoteControlViewModel_TabSwitchingAndUserManagement_WorksAsExpected()
    {
        var settingsService = new FakeSettingsService();
        var authService = new RemoteAuthenticationService();
        var netService = new LocalNetworkAddressService();

        var vm = new RemoteControlViewModel(settingsService, authService, netService, dashboardHost: null);

        Assert.True(vm.IsGeneralTab);
        Assert.False(vm.IsUsersTab);

        vm.IsUsersTab = true;
        Assert.False(vm.IsGeneralTab);
        Assert.True(vm.IsUsersTab);

        Assert.NotNull(vm.Users);
        Assert.NotNull(vm.AddUserCommand);
        Assert.NotNull(vm.RemoveUserCommand);

        vm.AddUserCommand.Execute(null);
        Assert.Single(vm.Users);

        var user = vm.Users[0];
        Assert.NotNull(user);
        user.Username = "Operator1";
        user.AllowRemoteConsoleCommands = true;

        vm.RemoveUserCommand.Execute(user);
        Assert.Empty(vm.Users);
    }
}
