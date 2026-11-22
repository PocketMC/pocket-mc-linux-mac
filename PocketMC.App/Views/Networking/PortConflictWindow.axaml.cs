using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PocketMC.Core.Models.Networking;
using PocketMC.Infrastructure.Networking;

namespace PocketMC.App.Views.Networking;

public partial class PortConflictWindow : Window
{
    private readonly int _currentPort;
    private readonly PortCheckRequest? _request;
    private readonly int? _suggestedPort;
    private readonly PortProbeService? _probeService;
    private readonly PortLeaseRegistry? _leaseRegistry;

    public int? NewPort { get; private set; }
    public bool UserConfirmed { get; private set; }

    public PortConflictWindow()
    {
        InitializeComponent();
    }

    public PortConflictWindow(
        string title,
        string message,
        PortCheckRequest request,
        int? suggestedPort,
        PortProbeService? probeService = null,
        PortLeaseRegistry? leaseRegistry = null)
    {
        _request = request ?? throw new ArgumentNullException(nameof(request));
        _currentPort = request.Port;
        _suggestedPort = suggestedPort;
        _probeService = probeService;
        _leaseRegistry = leaseRegistry;

        InitializeComponent();

        TxtTitle.Text = title;
        TxtMessage.Text = message;

        if (_suggestedPort.HasValue && _suggestedPort.Value != _currentPort)
        {
            BorderSuggestedPort.IsVisible = true;
            string protoText = _request.Protocol == PortProtocol.Udp ? "UDP" : "TCP";
            TxtSuggestedPortDescription.Text = $"Port {_suggestedPort.Value} ({protoText}) is verified available";
            TxtPortInput.Text = _suggestedPort.Value.ToString();
        }
        else
        {
            TxtPortInput.Text = string.Empty;
        }

        BtnApplySuggested.Click += BtnApplySuggested_Click;
        TxtPortInput.PropertyChanged += (s, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                ValidateCurrentInput();
            }
        };

        BtnCancel.Click += (s, e) =>
        {
            UserConfirmed = false;
            Close();
        };

        BtnConfirm.Click += (s, e) =>
        {
            if (int.TryParse(TxtPortInput.Text?.Trim(), out int port) && port >= 1024 && port <= 65535)
            {
                NewPort = port;
                UserConfirmed = true;
                Close();
            }
        };

        ValidateCurrentInput();
    }

    private void BtnApplySuggested_Click(object? sender, RoutedEventArgs e)
    {
        if (_suggestedPort.HasValue)
        {
            TxtPortInput.Text = _suggestedPort.Value.ToString();
            ValidateCurrentInput();
        }
    }

    private void ValidateCurrentInput()
    {
        string raw = TxtPortInput.Text?.Trim() ?? string.Empty;
        TxtValidation.IsVisible = false;
        TxtSuccess.IsVisible = false;
        BtnConfirm.IsEnabled = false;

        if (string.IsNullOrEmpty(raw))
        {
            return;
        }

        if (!int.TryParse(raw, out int port) || port < 1 || port > 65535)
        {
            TxtValidation.Text = "Port must be an integer between 1 and 65535.";
            TxtValidation.IsVisible = true;
            return;
        }

        if (port < 1024)
        {
            TxtValidation.Text = "Ports below 1024 are privileged and may require root/admin permissions.";
            TxtValidation.IsVisible = true;
            return;
        }

        if (port == _currentPort)
        {
            TxtValidation.Text = "This is the current conflicting port.";
            TxtValidation.IsVisible = true;
            return;
        }

        if (_leaseRegistry != null && _leaseRegistry.IsPortLeased(port, _request?.Protocol ?? PortProtocol.Tcp, _request?.IpMode ?? PortIpMode.IPv4, _request?.BindAddress))
        {
            TxtValidation.Text = $"Port {port} is already reserved by another PocketMC instance.";
            TxtValidation.IsVisible = true;
            return;
        }

        if (_probeService != null)
        {
            var probeResult = _probeService.Probe(new PortCheckRequest(
                port,
                _request?.Protocol ?? PortProtocol.Tcp,
                _request?.IpMode ?? PortIpMode.IPv4,
                _request?.BindAddress));

            if (!probeResult.CanBindLocally || !probeResult.IsSuccessful)
            {
                TxtValidation.Text = probeResult.FailureMessage ?? $"Port {port} is already in use on this machine.";
                TxtValidation.IsVisible = true;
                return;
            }
        }

        TxtSuccess.Text = $"Port {port} is available!";
        TxtSuccess.IsVisible = true;
        BtnConfirm.IsEnabled = true;
    }
}
