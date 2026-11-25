using Avalonia.Controls;
using Avalonia.Input;
using PocketMC.App.ViewModels;

namespace PocketMC.App.Views.Marketplace;

public partial class PluginBrowserPage : UserControl
{
    public PluginBrowserPage()
    {
        InitializeComponent();
        KeyDown += OnPageKeyDown;
    }

    private async void OnPageKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            this.FindControl<TextBox>("TxtSearch")?.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.F5 || (e.Key == Key.R && e.KeyModifiers.HasFlag(KeyModifiers.Control)))
        {
            if (DataContext is MarketplaceViewModel vm)
            {
                await vm.SearchCommand.ExecuteAsync(null);
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            var searchBox = this.FindControl<TextBox>("TxtSearch");
            if (searchBox != null && !string.IsNullOrEmpty(searchBox.Text) && DataContext is MarketplaceViewModel vm)
            {
                vm.SearchQuery = string.Empty;
                e.Handled = true;
            }
        }
    }
}
