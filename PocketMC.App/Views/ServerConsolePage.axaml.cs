using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using PocketMC.App.Controls;
using PocketMC.App.Services;
using PocketMC.App.ViewModels;

namespace PocketMC.App.Views
{
    public partial class ServerConsolePage : UserControl
    {
        private ServerConsoleViewModel? _boundViewModel;

        public ServerConsolePage()
        {
            InitializeComponent();

            DataContextChanged += (_, _) =>
            {
                if (_boundViewModel != null)
                {
                    _boundViewModel.LogLines.CollectionChanged -= OnLogLinesChanged;
                    _boundViewModel.PropertyChanged -= OnViewModelPropertyChanged;
                }

                _boundViewModel = DataContext as ServerConsoleViewModel;
                if (_boundViewModel != null)
                {
                    _boundViewModel.LogLines.CollectionChanged += OnLogLinesChanged;
                    _boundViewModel.PropertyChanged += OnViewModelPropertyChanged;
                    UpdateAiMarkdown(_boundViewModel.AiMarkdown);
                    if (_boundViewModel.AutoScroll)
                    {
                        ScrollToEnd();
                    }
                }
            };
        }

        public ServerConsolePage(ServerConsoleViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ServerConsoleViewModel.AiMarkdown) && _boundViewModel != null)
            {
                Dispatcher.UIThread.Post(() => UpdateAiMarkdown(_boundViewModel.AiMarkdown));
            }
        }

        private void UpdateAiMarkdown(string? markdown)
        {
            var host = this.FindControl<ContentControl>("AiMarkdownHost");
            if (host == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(markdown))
            {
                host.Content = new TextBlock
                {
                    Text = "Select a session log or right-click a log line to generate an AI summary or troubleshooting report.",
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    FontSize = 12,
                    Opacity = 0.7
                };
                return;
            }

            host.Content = MarkdownRenderer.Render(markdown);
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

        private void OnAnalyzeLogLineClick(object? sender, RoutedEventArgs e)
        {
            if (_boundViewModel == null) return;
            if (sender is MenuItem { DataContext: LogLine line })
            {
                _boundViewModel.AnalyzeSnippetCommand.Execute(line.Text);
            }
        }

        private async void OnCopyLogLineClick(object? sender, RoutedEventArgs e)
        {
            if (sender is MenuItem { DataContext: LogLine line })
            {
                await AppClipboard.SetTextAsync(line.Text);
            }
        }
    }
}
