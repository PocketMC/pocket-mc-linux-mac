using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PocketMC.Core.Models;
using PocketMC.Core.Services;

namespace PocketMC.App.ViewModels;

public partial class OllamaModelManagerViewModel : ObservableObject
{
    private readonly IOllamaService _ollamaService;
    private readonly string _endpoint;
    private readonly string? _apiKey;
    private CancellationTokenSource? _pullCts;

    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private string _statusText = "Checking installed models...";
    [ObservableProperty] private string _customModelName = string.Empty;
    [ObservableProperty] private bool _isDownloading;
    [ObservableProperty] private string _progressStatusText = string.Empty;
    [ObservableProperty] private double _downloadPercent;
    [ObservableProperty] private string _manualCommand = "ollama pull llama3.2:3b";

    public string? SelectedModel { get; private set; }

    public ObservableCollection<OllamaModelInfo> InstalledModels { get; } = new();

    public IAsyncRelayCommand RefreshModelsCommand { get; }
    public IRelayCommand<string> SelectModelCommand { get; }
    public IAsyncRelayCommand<string> DeleteModelCommand { get; }
    public IAsyncRelayCommand PullCustomModelCommand { get; }
    public IAsyncRelayCommand<string> PullRecommendedModelCommand { get; }
    public IRelayCommand CancelPullCommand { get; }
    public IAsyncRelayCommand CopyManualCommandCommand { get; }

    public event Action<string?>? CloseRequested;

    public OllamaModelManagerViewModel(IOllamaService ollamaService, string endpoint, string? apiKey = null)
    {
        _ollamaService = ollamaService;
        _endpoint = string.IsNullOrWhiteSpace(endpoint) ? "http://localhost:11434" : endpoint;
        _apiKey = apiKey;

        RefreshModelsCommand = new AsyncRelayCommand(RefreshModelsAsync);
        SelectModelCommand = new RelayCommand<string>(SelectModel);
        DeleteModelCommand = new AsyncRelayCommand<string>(DeleteModelAsync);
        PullCustomModelCommand = new AsyncRelayCommand(() => PullModelAsync(CustomModelName));
        PullRecommendedModelCommand = new AsyncRelayCommand<string>(m => PullModelAsync(m ?? string.Empty));
        CancelPullCommand = new RelayCommand(() => _pullCts?.Cancel());
        CopyManualCommandCommand = new AsyncRelayCommand(CopyManualCommandAsync);
    }

    public async Task RefreshModelsAsync()
    {
        StatusText = "Loading models...";
        var models = await _ollamaService.GetInstalledModelsAsync(_endpoint, _apiKey);
        Dispatcher.UIThread.Post(() =>
        {
            InstalledModels.Clear();
            foreach (var model in models)
            {
                InstalledModels.Add(model);
            }
            StatusText = InstalledModels.Count > 0
                ? $"{InstalledModels.Count} model(s) installed at {_endpoint}"
                : "No local models found. Use the Download Models tab to pull one.";
        });
    }

    private void SelectModel(string? modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName)) return;
        SelectedModel = modelName;
        CloseRequested?.Invoke(modelName);
    }

    private async Task DeleteModelAsync(string? modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName)) return;
        try
        {
            await _ollamaService.DeleteModelAsync(_endpoint, modelName, _apiKey);
            await RefreshModelsAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Delete failed: {ex.Message}";
        }
    }

    private async Task PullModelAsync(string modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName) || IsDownloading) return;

        _pullCts?.Dispose();
        _pullCts = new CancellationTokenSource();
        IsDownloading = true;
        DownloadPercent = 0;
        ProgressStatusText = $"Starting download of {modelName}...";

        try
        {
            var progress = new Progress<OllamaPullProgress>(p =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    DownloadPercent = p.Percent;
                    ProgressStatusText = string.IsNullOrWhiteSpace(p.ErrorMessage)
                        ? $"{p.Status} ({p.Percent:F0}%)"
                        : p.ErrorMessage;
                });
            });

            await _ollamaService.PullModelAsync(_endpoint, modelName.Trim(), _apiKey, progress, _pullCts.Token);
            SelectedTabIndex = 0;
            await RefreshModelsAsync();
        }
        catch (OperationCanceledException)
        {
            ProgressStatusText = "Download cancelled.";
        }
        catch (Exception ex)
        {
            ProgressStatusText = $"Pull failed: {ex.Message}";
        }
        finally
        {
            IsDownloading = false;
        }
    }

    private async Task CopyManualCommandAsync()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow?.Clipboard != null)
        {
            await desktop.MainWindow.Clipboard.SetTextAsync(ManualCommand);
        }
    }
}
