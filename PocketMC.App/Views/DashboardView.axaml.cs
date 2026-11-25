using Avalonia.Controls;
using Avalonia.Input;
using PocketMC.App.ViewModels;

namespace PocketMC.App.Views
{
    public partial class DashboardView : UserControl
    {
        public DashboardView()
        {
            InitializeComponent();
        }

        private void InstanceCard_OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter && e.Key != Key.Space)
            {
                return;
            }

            if (sender is Control control &&
                control.DataContext is InstanceCardViewModel card &&
                DataContext is DashboardViewModel vm)
            {
                if (card.IsRunning)
                {
                    vm.OpenConsoleCommand.Execute(card);
                }
                else
                {
                    vm.StartServerCommand.Execute(card);
                }
                e.Handled = true;
            }
        }
    }
}
