using System;
using System.Collections.Generic;
using PocketMC.App.ViewModels;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.RemoteControl.Services;
using PocketMC.RemoteControl.Tunnels;
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
        var tunnelManager = new RemoteTunnelManager(Array.Empty<IRemoteTunnelProvider>(), settingsService);
        var coordinator = new RemoteControlCoordinator(settingsService, null, tunnelManager, netService);

        var vm = new RemoteControlViewModel(settingsService, coordinator, authService, netService);

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

    [Fact]
    public void RemoteControlViewModel_SaveCredentials_StoresHashedPassword()
    {
        var settingsService = new FakeSettingsService();
        var authService = new RemoteAuthenticationService();
        var netService = new LocalNetworkAddressService();
        var tunnelManager = new RemoteTunnelManager(Array.Empty<IRemoteTunnelProvider>(), settingsService);
        var coordinator = new RemoteControlCoordinator(settingsService, null, tunnelManager, netService);

        var vm = new RemoteControlViewModel(settingsService, coordinator, authService, netService);

        vm.Username = "admin";
        vm.Password = "SuperSecret123!";
        vm.SaveCredentialsCommand.Execute(null);

        Assert.True(vm.IsPasswordSet);
        Assert.False(vm.IsPasswordNotSet);
        Assert.Equal("admin", settingsService.Settings.RemoteControl.Username);
        Assert.NotNull(settingsService.Settings.RemoteControl.PasswordHash);
        Assert.True(authService.VerifyPassword("SuperSecret123!", settingsService.Settings.RemoteControl.PasswordHash));
    }

    [Fact]
    public void RemoteControlViewModel_AccessModeChange_UpdatesSettings()
    {
        var settingsService = new FakeSettingsService();
        var authService = new RemoteAuthenticationService();
        var netService = new LocalNetworkAddressService();
        var tunnelManager = new RemoteTunnelManager(Array.Empty<IRemoteTunnelProvider>(), settingsService);
        var coordinator = new RemoteControlCoordinator(settingsService, null, tunnelManager, netService);

        var vm = new RemoteControlViewModel(settingsService, coordinator, authService, netService);

        vm.AccessMode = RemoteAccessMode.PlayitHttpsTunnel;
        Assert.Equal(RemoteAccessMode.PlayitHttpsTunnel, settingsService.Settings.RemoteControl.AccessMode);
        Assert.Equal("playit-https", settingsService.Settings.RemoteControl.TunnelProviderId);
    }
}
