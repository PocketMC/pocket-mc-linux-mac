using System;
using CommunityToolkit.Mvvm.ComponentModel;
using PocketMC.Core.Models;

namespace PocketMC.App.ViewModels;

public partial class RemoteControlUserViewModel : ObservableObject
{
    public RemoteControlUser Model { get; }

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _allowRemoteConsoleCommands;

    [ObservableProperty]
    private bool _allowRemotePlayerActions;

    [ObservableProperty]
    private bool _allowRemoteServerSettings;

    [ObservableProperty]
    private bool _allowRemoteServerAddons;

    [ObservableProperty]
    private bool _allowRemoteFileManager;

    [ObservableProperty]
    private bool _allowRemoteServerBackups;

    [ObservableProperty]
    private bool _allowAllInstances = true;

    [ObservableProperty]
    private bool _isEditing;

    public RemoteControlUserViewModel(RemoteControlUser model)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        _username = model.Username ?? string.Empty;
        _allowRemoteConsoleCommands = model.AllowRemoteConsoleCommands;
        _allowRemotePlayerActions = model.AllowRemotePlayerActions;
        _allowRemoteServerSettings = model.AllowRemoteServerSettings;
        _allowRemoteServerAddons = model.AllowRemoteServerAddons;
        _allowRemoteFileManager = model.AllowRemoteFileManager;
        _allowRemoteServerBackups = model.AllowRemoteServerBackups;
        _allowAllInstances = model.AllowAllInstances;
    }

    public void ApplyToModel()
    {
        Model.Username = Username;
        Model.AllowRemoteConsoleCommands = AllowRemoteConsoleCommands;
        Model.AllowRemotePlayerActions = AllowRemotePlayerActions;
        Model.AllowRemoteServerSettings = AllowRemoteServerSettings;
        Model.AllowRemoteServerAddons = AllowRemoteServerAddons;
        Model.AllowRemoteFileManager = AllowRemoteFileManager;
        Model.AllowRemoteServerBackups = AllowRemoteServerBackups;
        Model.AllowAllInstances = AllowAllInstances;
    }
}
