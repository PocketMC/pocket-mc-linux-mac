using System;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using PocketMC.RemoteControl.Tunnels;

namespace PocketMC.App.Views.Tunnel;

public partial class CreateTunnelDialog : Window
{
    private static readonly Regex AsciiOnlyRegex = new(@"^[\x20-\x7E]+$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));
    private readonly PlayitApiClient? _playitApiClient;

    public bool TunnelCreated { get; private set; }

    public CreateTunnelDialog()
    {
        InitializeComponent();
    }

    public CreateTunnelDialog(PlayitApiClient playitApiClient)
    {
        _playitApiClient = playitApiClient ?? throw new ArgumentNullException(nameof(playitApiClient));

        InitializeComponent();

        BtnCancel.Click += (s, e) =>
        {
            TunnelCreated = false;
            Close();
        };

        BtnCreate.Click += async (s, e) =>
        {
            if (!ValidateForm())
            {
                return;
            }

            BtnCreate.IsEnabled = false;
            BtnCancel.IsEnabled = false;

            string name = TxtTunnelName.Text!.Trim();
            string type = (CmbTunnelType.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "minecraft-java";
            int port = int.Parse(TxtLocalPort.Text!.Trim());

            try
            {
                var result = await _playitApiClient.CreateTunnelAsync(name, type, port);
                if (result.Success)
                {
                    TunnelCreated = true;
                    Close();
                    return;
                }

                TxtNameError.Text = result.ErrorMessage ?? "Failed to create tunnel with Playit.";
                TxtNameError.IsVisible = true;
            }
            catch (Exception ex)
            {
                TxtNameError.Text = $"Error: {ex.Message}";
                TxtNameError.IsVisible = true;
            }
            finally
            {
                BtnCreate.IsEnabled = true;
                BtnCancel.IsEnabled = true;
            }
        };
    }

    private bool ValidateForm()
    {
        bool valid = true;

        string name = TxtTunnelName.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            TxtNameError.Text = "Tunnel name is required.";
            TxtNameError.IsVisible = true;
            valid = false;
        }
        else if (!AsciiOnlyRegex.IsMatch(name))
        {
            TxtNameError.Text = "Tunnel name must contain ASCII characters only.";
            TxtNameError.IsVisible = true;
            valid = false;
        }
        else
        {
            TxtNameError.IsVisible = false;
        }

        string portText = TxtLocalPort.Text?.Trim() ?? string.Empty;
        if (!int.TryParse(portText, out int port) || port < 1 || port > 65535)
        {
            TxtPortError.Text = "Enter a valid port number (1-65535).";
            TxtPortError.IsVisible = true;
            valid = false;
        }
        else
        {
            TxtPortError.IsVisible = false;
        }

        return valid;
    }
}
