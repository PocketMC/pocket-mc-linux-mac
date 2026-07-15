using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace PocketMC.App.Views;

public partial class TunnelPage : UserControl
{
    public TunnelPage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
