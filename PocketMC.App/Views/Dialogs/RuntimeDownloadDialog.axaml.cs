using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PocketMC.App.Views.Dialogs;

public partial class RuntimeDownloadDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Downloading Runtime";

    [ObservableProperty]
    private string _statusMessage = "Preparing download...";

    [ObservableProperty]
    private double _progressPercentage;

    [ObservableProperty]
    private bool _isIndeterminate = true;
}

public partial class RuntimeDownloadDialog : Window
{
    public RuntimeDownloadDialogViewModel ViewModel { get; }

    public RuntimeDownloadDialog()
    {
        ViewModel = new RuntimeDownloadDialogViewModel();
        DataContext = ViewModel;
        InitializeComponent();
    }

    public RuntimeDownloadDialog(string title, string initialMessage) : this()
    {
        ViewModel.Title = title;
        ViewModel.StatusMessage = initialMessage;
    }
}
