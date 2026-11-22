using Avalonia.Controls;

namespace PocketMC.App.Views
{
    public partial class NewInstancePage : UserControl
    {
        public NewInstancePage()
        {
            InitializeComponent();
        }

        public NewInstancePage(PocketMC.App.ViewModels.NewInstanceViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}
