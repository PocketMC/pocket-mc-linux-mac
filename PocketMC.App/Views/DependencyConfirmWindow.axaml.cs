using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PocketMC.Infrastructure.Services;

namespace PocketMC.App.Views
{
    public partial class DependencyConfirmWindow : Window
    {
        public DependencyConfirmWindow()
        {
            InitializeComponent();
        }

        public DependencyConfirmWindow(List<ResolvedDependency> dependencies) : this()
        {
            var lst = this.FindControl<ListBox>("LstDependencies");
            if (lst != null)
            {
                lst.ItemsSource = dependencies;
            }
        }

        private void BtnCancel_Click(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }

        private void BtnProceed_Click(object? sender, RoutedEventArgs e)
        {
            Close(true);
        }
    }
}
