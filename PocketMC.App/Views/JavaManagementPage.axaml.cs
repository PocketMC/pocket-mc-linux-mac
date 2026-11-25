using Avalonia.Controls;
using Avalonia.Interactivity;
using PocketMC.App.ViewModels;

namespace PocketMC.App.Views;

public partial class JavaManagementPage : UserControl
{
    public JavaManagementPage()
    {
        InitializeComponent();
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (DataContext is JavaManagementViewModel vm)
        {
            await vm.RefreshAsync();
        }
    }
}
