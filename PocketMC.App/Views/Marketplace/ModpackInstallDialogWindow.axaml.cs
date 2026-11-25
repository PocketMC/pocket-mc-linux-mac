using System;
using Avalonia.Controls;
using PocketMC.App.ViewModels;

namespace PocketMC.App.Views.Marketplace;

public partial class ModpackInstallDialogWindow : Window
{
    public ModpackInstallDialogWindow()
    {
        InitializeComponent();
    }

    public ModpackInstallDialogWindow(ModpackInstallViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += Close;
        Opened += async (_, _) => await viewModel.StartInstallAsync();
    }
}
