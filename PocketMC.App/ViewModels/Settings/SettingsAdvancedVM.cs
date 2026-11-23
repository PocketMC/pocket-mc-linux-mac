using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PocketMC.App.ViewModels.Settings
{
    public partial class SettingsAdvancedVM : ObservableObject
    {
        private string _serverDir = string.Empty;
        private readonly Action _markDirty;

        [ObservableProperty] private bool _enableAutoRestart;
        [ObservableProperty] private string _maxAutoRestarts = "3";
        [ObservableProperty] private string _autoRestartDelay = "10";

        // Scheduled Maintenance
        [ObservableProperty] private bool _enableScheduledReboot;
        [ObservableProperty] private string _scheduledRebootMode = "Daily";
        [ObservableProperty] private string _scheduledRebootTime = "04:00";
        [ObservableProperty] private int _scheduledRebootIntervalHours = 24;
        [ObservableProperty] private bool _scheduledRebootWarning = true;
        [ObservableProperty] private int _scheduledRebootWarningSeconds = 60;

        public bool IsDailyMode => string.Equals(ScheduledRebootMode, "Daily", StringComparison.OrdinalIgnoreCase);
        public bool IsIntervalMode => string.Equals(ScheduledRebootMode, "Interval", StringComparison.OrdinalIgnoreCase);
        public bool IsWarningSettingsEnabled => EnableScheduledReboot && ScheduledRebootWarning;

        public List<string> RebootModes { get; } = new() { "Daily", "Interval" };

        // Server Properties & Ports
        [ObservableProperty] private bool _pvp = true;
        [ObservableProperty] private bool _allowFlight;
        [ObservableProperty] private bool _enableCommandBlock = true;
        [ObservableProperty] private bool _enableQuery;
        [ObservableProperty] private int _queryPort = 25565;
        [ObservableProperty] private bool _enableRcon;
        [ObservableProperty] private string _rconPassword = string.Empty;
        [ObservableProperty] private int _rconPort = 25575;
        [ObservableProperty] private string _resourcePack = string.Empty;

        // Raw Properties
        [ObservableProperty] private string _rawPropertiesText = string.Empty;

        public SettingsAdvancedVM(Action markDirty)
        {
            _markDirty = markDirty;
        }

        public void UpdateServerDir(string newDir)
        {
            _serverDir = newDir;
        }

        partial void OnEnableAutoRestartChanged(bool value) => _markDirty();
        partial void OnMaxAutoRestartsChanged(string value) => _markDirty();
        partial void OnAutoRestartDelayChanged(string value) => _markDirty();

        partial void OnEnableScheduledRebootChanged(bool value)
        {
            OnPropertyChanged(nameof(IsDailyMode));
            OnPropertyChanged(nameof(IsIntervalMode));
            OnPropertyChanged(nameof(IsWarningSettingsEnabled));
            _markDirty();
        }

        partial void OnScheduledRebootModeChanged(string value)
        {
            OnPropertyChanged(nameof(IsDailyMode));
            OnPropertyChanged(nameof(IsIntervalMode));
            _markDirty();
        }

        partial void OnScheduledRebootTimeChanged(string value) => _markDirty();
        partial void OnScheduledRebootIntervalHoursChanged(int value) => _markDirty();
        partial void OnScheduledRebootWarningChanged(bool value)
        {
            OnPropertyChanged(nameof(IsWarningSettingsEnabled));
            _markDirty();
        }
        partial void OnScheduledRebootWarningSecondsChanged(int value) => _markDirty();

        partial void OnPvpChanged(bool value) => _markDirty();
        partial void OnAllowFlightChanged(bool value) => _markDirty();
        partial void OnEnableCommandBlockChanged(bool value) => _markDirty();
        partial void OnEnableQueryChanged(bool value) => _markDirty();
        partial void OnQueryPortChanged(int value) => _markDirty();
        partial void OnEnableRconChanged(bool value) => _markDirty();
        partial void OnRconPasswordChanged(string value) => _markDirty();
        partial void OnRconPortChanged(int value) => _markDirty();
        partial void OnResourcePackChanged(string value) => _markDirty();
        partial void OnRawPropertiesTextChanged(string value) => _markDirty();
    }
}
