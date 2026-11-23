using Avalonia.Controls;
using PocketMC.App.ViewModels;

namespace PocketMC.App.Views
{
    public partial class NewsPage : UserControl
    {
        public NewsPage()
        {
            InitializeComponent();
        }

        public NewsPage(NewsViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}
