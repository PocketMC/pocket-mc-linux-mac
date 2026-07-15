using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using PocketMC.App.ViewModels;

namespace PocketMC.App.Views
{
    public partial class ServerConsolePage : UserControl
    {
        private ServerConsoleViewModel? _boundViewModel;

        public ServerConsolePage()
        {
            InitializeComponent();

            DataContextChanged += (sender, args) =>
            {
                if (_boundViewModel != null)
                {
                    _boundViewModel.LogLines.CollectionChanged -= OnLogLinesChanged;
                }
                _boundViewModel = DataContext as ServerConsoleViewModel;
                if (_boundViewModel != null)
                {
                    _boundViewModel.LogLines.CollectionChanged += OnLogLinesChanged;
                    if (_boundViewModel.AutoScroll)
                    {
                        ScrollToEnd();
                    }
                }
            };
        }

        private void OnLogLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_boundViewModel != null && _boundViewModel.AutoScroll)
            {
                ScrollToEnd();
            }
        }

        private void ScrollToEnd()
        {
            var scrollViewer = this.FindControl<ScrollViewer>("LogScrollViewer");
            if (scrollViewer != null)
            {
                Dispatcher.UIThread.Post(() => scrollViewer.ScrollToEnd(), DispatcherPriority.Background);
            }
        }

        private void OnCommandBoxKeyDown(object? sender, KeyEventArgs e)
        {
            if (_boundViewModel == null) return;

            if (e.Key == Key.Enter)
            {
                _boundViewModel.SendCommandCommand.Execute(null);
            }
            else if (e.Key == Key.Up)
            {
                _boundViewModel.CycleHistory(true);
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                _boundViewModel.CycleHistory(false);
                e.Handled = true;
            }
        }
    }
}
