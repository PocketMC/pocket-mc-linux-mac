using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PocketMC.App.ViewModels.Marketplace;
using PocketMC.Infrastructure.Services;

namespace PocketMC.App.Views;

public partial class DependencyConfirmWindow : Window
{
    public DependencyConfirmationViewModel? ViewModel { get; }

    public DependencyConfirmWindow()
    {
        InitializeComponent();
    }

    public DependencyConfirmWindow(List<ResolvedDependency> dependencies)
        : this(new DependencyConfirmationViewModel(dependencies))
    {
    }

    public DependencyConfirmWindow(DependencyConfirmationViewModel viewModel) : this()
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        viewModel.CloseRequested += () => Close(viewModel.Result == true);
    }

    private void BtnCancel_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.CancelCommand.Execute(null);
        }
        else
        {
            Close(false);
        }
    }

    private void BtnProceed_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.ConfirmCommand.Execute(null);
        }
        else
        {
            Close(true);
        }
    }
}
