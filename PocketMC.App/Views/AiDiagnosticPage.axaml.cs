using System.ComponentModel;
using Avalonia.Controls;
using PocketMC.App.Controls;
using PocketMC.App.ViewModels;

namespace PocketMC.App.Views
{
    public partial class AiDiagnosticPage : UserControl
    {
        public AiDiagnosticPage()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object? sender, System.EventArgs e)
        {
            if (DataContext is AiDiagnosticViewModel vm)
            {
                vm.PropertyChanged += OnViewModelPropertyChanged;
                UpdateMarkdownContent(vm.AnalysisResult);
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AiDiagnosticViewModel.AnalysisResult))
            {
                var vm = (AiDiagnosticViewModel)sender!;
                UpdateMarkdownContent(vm.AnalysisResult);
            }
        }

        private void UpdateMarkdownContent(string markdown)
        {
            var host = this.FindControl<ContentControl>("MarkdownHost");
            if (host != null)
            {
                if (string.IsNullOrEmpty(markdown))
                {
                    host.Content = null;
                }
                else
                {
                    host.Content = MarkdownRenderer.Render(markdown);
                }
            }
        }
    }
}
