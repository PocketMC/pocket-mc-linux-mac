using Avalonia.Controls;
using PocketMC.App.ViewModels;

namespace PocketMC.App.Views
{
    public partial class PortsMapPage : UserControl
    {
        public PortsMapPage()
        {
            InitializeComponent();
        }

        public PortsMapPage(PortsMapViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        protected override async void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
        {
            base.OnLoaded(e);
            if (DataContext is PortsMapViewModel vm)
            {
                await vm.RefreshMapAsync();
            }
        }
    }
}
