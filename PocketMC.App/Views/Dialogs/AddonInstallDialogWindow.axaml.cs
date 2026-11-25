using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using PocketMC.App.ViewModels.Marketplace;

namespace PocketMC.App.Views.Dialogs;

public readonly record struct AddonInstallProgress(double Percentage, long BytesRead, long TotalBytes);

public partial class AddonInstallDialogWindow : Window
{
    private readonly ObservableCollection<AddonInstallRowViewModel> _items = new();
    private CancellationTokenSource? _cts;
    private bool _isRunning;
    private bool _started;

    public Func<AddonInstallRowViewModel, IProgress<AddonInstallProgress>, CancellationToken, Task>? InstallAction { get; set; }
    public Action? OnAllInstallsCompleted { get; set; }

    public bool AnyInstalled { get; private set; }
    public int InstalledCount { get; private set; }
    public int FailedCount { get; private set; }

    public AddonInstallDialogWindow()
    {
        InitializeComponent();
        var list = this.FindControl<ItemsControl>("ItemsList");
        if (list != null)
        {
            list.ItemsSource = _items;
        }
    }

    public void SetItems(IEnumerable<AddonInstallRowViewModel> items)
    {
        _items.Clear();
        foreach (var item in items)
        {
            _items.Add(item);
        }
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (_started) return;
        _started = true;

        await Task.Delay(150);

        if (_items.Count == 0 || InstallAction == null)
        {
            Close();
            return;
        }

        _isRunning = true;
        _cts = new CancellationTokenSource();

        var overallLabel = this.FindControl<TextBlock>("TxtOverallStatus");
        var overallBar = this.FindControl<ProgressBar>("OverallProgressBar");
        var cancelBtn = this.FindControl<Button>("BtnCancel");
        if (cancelBtn != null) cancelBtn.Content = "Cancel";

        int total = _items.Count;
        int current = 0;
        int succeeded = 0;
        int failed = 0;

        foreach (var item in _items)
        {
            if (_cts.Token.IsCancellationRequested) break;

            current++;
            if (overallLabel != null) overallLabel.Text = $"Installing {current}/{total}: {item.ProjectTitle}...";
            if (overallBar != null) overallBar.Value = (double)(current - 1) / total * 100.0;

            item.StatusText = "Downloading...";
            item.StatusForeground = "#89B4FA";
            item.IsDownloading = true;
            item.ProgressValue = 0;

            var progress = new Progress<AddonInstallProgress>(dp =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    item.ProgressValue = dp.Percentage;
                    if (dp.TotalBytes > 0)
                    {
                        item.StatusText = $"Downloading... {FormatBytes(dp.BytesRead)} / {FormatBytes(dp.TotalBytes)}";
                    }
                    else if (dp.Percentage > 0)
                    {
                        item.StatusText = $"Downloading... {dp.Percentage:F0}%";
                    }
                });
            });

            try
            {
                await Task.Run(async () => await InstallAction(item, progress, _cts.Token));
                item.ProgressValue = 100;
                item.IsDownloading = false;
                item.StatusText = "Installed successfully";
                item.StatusForeground = "#10B981";
                succeeded++;
            }
            catch (OperationCanceledException)
            {
                item.IsDownloading = false;
                item.StatusText = "Cancelled";
                item.StatusForeground = "#F59E0B";
                break;
            }
            catch (Exception ex)
            {
                item.IsDownloading = false;
                item.StatusText = $"Failed: {ex.Message}";
                item.StatusForeground = "#EF4444";
                failed++;
            }
        }

        InstalledCount = succeeded;
        FailedCount = failed;
        AnyInstalled = succeeded > 0;

        if (overallBar != null) overallBar.Value = 100;
        if (overallLabel != null)
        {
            if (_cts.Token.IsCancellationRequested)
            {
                overallLabel.Text = $"Cancelled. {succeeded} installed, {total - succeeded - failed} skipped.";
            }
            else if (failed > 0)
            {
                overallLabel.Text = $"Done. {succeeded} installed, {failed} failed.";
            }
            else
            {
                overallLabel.Text = $"All {succeeded} addon(s) installed successfully!";
                overallLabel.Foreground = Brush.Parse("#10B981");
            }
        }

        if (cancelBtn != null) cancelBtn.Content = "Close";
        _isRunning = false;
        OnAllInstallsCompleted?.Invoke();
    }

    private void BtnCancel_Click(object? sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            _cts?.Cancel();
        }
        else
        {
            Close();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        base.OnClosed(e);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}
