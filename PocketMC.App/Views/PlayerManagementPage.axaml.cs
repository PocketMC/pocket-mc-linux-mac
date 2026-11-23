using Avalonia.Controls;

namespace PocketMC.App.Views
{
    public partial class PlayerManagementPage : UserControl
    {
        public PlayerManagementPage()
        {
            InitializeComponent();
        }

        public PlayerManagementPage(PocketMC.App.ViewModels.PlayerManagementViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}
