using Avalonia.Controls;
using PocketMC.App.ViewModels;

namespace PocketMC.App.Views;

public partial class StartupUpdateWindow : Window
{
    public StartupUpdateWindow()
    {
        InitializeComponent();
        DataContextChanged += (sender, args) =>
        {
            if (DataContext is StartupUpdateViewModel vm)
            {
                vm.RequestClose += () => Close();
            }
        };
    }
}
