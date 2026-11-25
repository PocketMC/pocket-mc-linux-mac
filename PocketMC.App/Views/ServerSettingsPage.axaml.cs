using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using PocketMC.App.Controls;
using PocketMC.App.ViewModels;

namespace PocketMC.App.Views
{
    public partial class ServerSettingsPage : UserControl
    {
        private ServerSettingsViewModel? _boundVm;

        public ServerSettingsPage()
        {
            InitializeComponent();
            DataContextChanged += (_, _) => HookViewModel(DataContext as ServerSettingsViewModel);
        }

        public ServerSettingsPage(ServerSettingsViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        private void HookViewModel(ServerSettingsViewModel? vm)
        {
            if (_boundVm != null)
            {
                _boundVm.Summaries.PropertyChanged -= OnSummariesPropertyChanged;
            }

            _boundVm = vm;
            if (_boundVm != null)
            {
                _boundVm.Summaries.PropertyChanged += OnSummariesPropertyChanged;
                UpdateSummaryMarkdown(_boundVm.Summaries.SummaryContent);
            }
        }

        private void OnSummariesPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModels.Settings.SettingsSummariesVM.SummaryContent) && _boundVm != null)
            {
                Dispatcher.UIThread.Post(() => UpdateSummaryMarkdown(_boundVm.Summaries.SummaryContent));
            }
        }

        private void UpdateSummaryMarkdown(string? markdown)
        {
            var host = this.FindControl<ContentControl>("SummaryMarkdownHost");
            if (host == null) return;

            host.Content = string.IsNullOrWhiteSpace(markdown)
                ? new TextBlock { Text = string.Empty }
                : MarkdownRenderer.Render(markdown);
        }

        private void BtnInsertMotdColor_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string code } && TxtMotdRaw != null)
            {
                string current = TxtMotdRaw.Text ?? string.Empty;
                int caret = Math.Clamp(TxtMotdRaw.CaretIndex, 0, current.Length);
                TxtMotdRaw.Text = current.Insert(caret, code);
                TxtMotdRaw.CaretIndex = caret + code.Length;
                TxtMotdRaw.Focus();
            }
        }
    }
}
