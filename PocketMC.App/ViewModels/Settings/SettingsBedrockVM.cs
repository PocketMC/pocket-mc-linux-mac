using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PocketMC.App.ViewModels.Settings
{
    public partial class SettingsBedrockVM : ObservableObject
    {
        private readonly Action _markDirty;

        [ObservableProperty]
        private string _serverPortV6 = "19133";

        [ObservableProperty]
        private bool _allowCheats;

        [ObservableProperty]
        private bool _texturepackRequired;

        [ObservableProperty]
        private bool _forceGamemode;

        [ObservableProperty]
        private string _defaultPlayerPermissionLevel = "member";

        [ObservableProperty]
        private string _tickDistance = "4";

        [ObservableProperty]
        private int _viewDistance = 32;

        public List<string> PermissionLevels { get; } = new() { "visitor", "member", "operator" };

        public SettingsBedrockVM(Action markDirty)
        {
            _markDirty = markDirty;
        }

        partial void OnServerPortV6Changed(string value) => _markDirty();
        partial void OnAllowCheatsChanged(bool value) => _markDirty();
        partial void OnTexturepackRequiredChanged(bool value) => _markDirty();
        partial void OnForceGamemodeChanged(bool value) => _markDirty();
        partial void OnDefaultPlayerPermissionLevelChanged(string value) => _markDirty();
        partial void OnTickDistanceChanged(string value) => _markDirty();
        partial void OnViewDistanceChanged(int value) => _markDirty();
    }
}
