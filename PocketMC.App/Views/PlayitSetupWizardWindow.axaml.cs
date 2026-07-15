using Avalonia.Controls;
using PocketMC.App.ViewModels;

namespace PocketMC.App.Views;

public partial class PlayitSetupWizardWindow : Window
{
    public PlayitSetupWizardWindow()
    {
        InitializeComponent();
        DataContextChanged += (sender, args) =>
        {
            if (DataContext is PlayitSetupWizardViewModel vm)
            {
                vm.RequestClose += () => Close();
            }
        };
    }
}
