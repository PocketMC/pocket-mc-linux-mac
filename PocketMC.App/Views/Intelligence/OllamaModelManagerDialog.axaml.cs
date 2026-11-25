using Avalonia.Controls;
using PocketMC.App.ViewModels;

namespace PocketMC.App.Views.Intelligence;

public partial class OllamaModelManagerDialog : Window
{
    public OllamaModelManagerDialog()
    {
        InitializeComponent();
    }

    public OllamaModelManagerDialog(OllamaModelManagerViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.CloseRequested += result => Close(result);
        Opened += async (_, _) => await viewModel.RefreshModelsAsync();
    }
}
