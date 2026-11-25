using Avalonia.Controls;
using Avalonia.Interactivity;
using PocketMC.App.ViewModels;

namespace PocketMC.App.Views;

public partial class PlayitStatusPage : UserControl
{
    public PlayitStatusPage()
    {
        InitializeComponent();
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (DataContext is PlayitStatusViewModel vm && vm.Monitors.Count == 0)
        {
            await vm.LoadStatusAsync();
        }
    }
}
