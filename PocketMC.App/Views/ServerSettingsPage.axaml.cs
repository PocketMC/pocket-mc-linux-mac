using Avalonia.Controls;

namespace PocketMC.App.Views
{
    public partial class ServerSettingsPage : UserControl
    {
        public ServerSettingsPage()
        {
            InitializeComponent();
        }

        public ServerSettingsPage(PocketMC.App.ViewModels.ServerSettingsViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}
