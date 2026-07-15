using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PocketMC.Core.Services;
using PocketMC.RemoteControl.Tunnels;
using Microsoft.Extensions.DependencyInjection;

namespace PocketMC.App.ViewModels;

public partial class PlayitSetupWizardViewModel : ObservableObject
{
    private readonly PlayitPartnerProvisioningClient _provisioningClient;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStep0))]
    [NotifyPropertyChangedFor(nameof(IsStep1))]
    [NotifyPropertyChangedFor(nameof(IsStep2))]
    [NotifyPropertyChangedFor(nameof(IsStep3))]
    private int _currentStep;

    public bool IsStep0 => CurrentStep == 0;
    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;

    [ObservableProperty]
    private string _setupCode = "";

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private string _agentId = "";

    [ObservableProperty]
    private bool _isConnecting;

    public event Action? RequestClose;

    public PlayitSetupWizardViewModel()
    {
        _provisioningClient = new PlayitPartnerProvisioningClient();
        _settingsService = App.Services.GetRequiredService<ISettingsService>();
        _currentStep = 1;
    }

    [RelayCommand]
    private void OpenSetupPage()
    {
        try
        {
            var uri = _provisioningClient.GetSetupPageUri();
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            });
            CurrentStep = 2; // Auto advance to pasting step
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to open browser: {ex.Message}";
            CurrentStep = 0;
        }
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (string.IsNullOrWhiteSpace(SetupCode))
        {
            ErrorMessage = "Please enter the setup code first.";
            CurrentStep = 0;
            return;
        }

        IsConnecting = true;
        try
        {
            var result = await _provisioningClient.CreateAgentAsync(SetupCode);
            if (result.Success && result.Response != null)
            {
                var conn = _settingsService.Settings.PlayitPartnerConnection ?? new();
                conn.SecretKey = result.Response.AgentSecretKey;
                conn.ClaimCode = SetupCode;
                conn.IsConfirmed = true;
                conn.AgentId = result.Response.AgentId;
                _settingsService.Settings.PlayitPartnerConnection = conn;
                _settingsService.Save();

                AgentId = result.Response.AgentId;
                CurrentStep = 3; // Success
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Failed to register Playit agent.";
                CurrentStep = 0;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            CurrentStep = 0;
        }
        finally
        {
            IsConnecting = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Finish()
    {
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Retry()
    {
        CurrentStep = 2; // Go back to paste code step
    }
}
