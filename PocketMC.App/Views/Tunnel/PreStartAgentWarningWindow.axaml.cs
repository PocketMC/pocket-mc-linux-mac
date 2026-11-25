using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PocketMC.App.Views.Tunnel;

public enum PreStartAgentChoice
{
    Cancel,
    StartAnyway,
    ConnectAgent
}

public partial class PreStartAgentWarningWindow : Window
{
    public PreStartAgentChoice Choice { get; private set; } = PreStartAgentChoice.Cancel;

    public PreStartAgentWarningWindow()
    {
        InitializeComponent();
    }

    private void BtnStartAnyway_Click(object? sender, RoutedEventArgs e)
    {
        Choice = PreStartAgentChoice.StartAnyway;
        Close(true);
    }

    private void BtnConnectAgent_Click(object? sender, RoutedEventArgs e)
    {
        Choice = PreStartAgentChoice.ConnectAgent;
        Close(false);
    }
}
