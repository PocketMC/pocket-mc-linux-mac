using Avalonia.Controls;
using PocketMC.App.ViewModels;

namespace PocketMC.App.Views
{
    public partial class RootDirectorySetupWindow : Window
    {
        public RootDirectorySetupWindow()
        {
            InitializeComponent();
        }

        public RootDirectorySetupWindow(RootDirectorySetupViewModel viewModel) : this()
        {
            DataContext = viewModel;
            viewModel.SetupCompleted += () =>
            {
                Close(true);
            };
        }
    }
}
