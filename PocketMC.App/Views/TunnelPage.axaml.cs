using Avalonia.Controls;
using Avalonia.Interactivity;
using PocketMC.App.ViewModels;

namespace PocketMC.App.Views;

public partial class TunnelPage : UserControl
{
    public TunnelPage()
    {
        InitializeComponent();
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (DataContext is TunnelViewModel vm && !vm.IsLoading)
        {
            await vm.RefreshAllAsync();
        }
    }
}
