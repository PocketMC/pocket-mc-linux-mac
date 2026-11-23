using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using PocketMC.RemoteControl.Tunnels;

namespace PocketMC.App.Views.Tunnel;

public partial class PlayitConsoleWindow : Window
{
    private readonly TunnelProcessGroupManager? _processManager;
    private readonly List<string> _allLogs = new();
    private readonly object _sync = new();
    private string _filterQuery = string.Empty;

    public PlayitConsoleWindow()
    {
        InitializeComponent();
    }

    public PlayitConsoleWindow(TunnelProcessGroupManager processManager)
    {
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));

        InitializeComponent();

        UpdateStatusBadge();

        lock (_sync)
        {
            _allLogs.AddRange(_processManager.GetRecentLogs());
        }

        RenderLogs();

        _processManager.OutputLineReceived += OnOutputLineReceived;

        Closed += (s, e) =>
        {
            if (_processManager != null)
            {
                _processManager.OutputLineReceived -= OnOutputLineReceived;
            }
        };

        BtnCopyLogs.Click += async (s, e) =>
        {
            var text = TxtLogs.Text;
            if (!string.IsNullOrEmpty(text) && Clipboard != null)
            {
                await Clipboard.SetTextAsync(text);
            }
        };

        BtnClearLogs.Click += (s, e) =>
        {
            lock (_sync)
            {
                _allLogs.Clear();
            }
            _processManager.ClearLogs();
            RenderLogs();
        };

        TxtSearch.PropertyChanged += (s, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                _filterQuery = TxtSearch.Text?.Trim() ?? string.Empty;
                RenderLogs();
            }
        };
    }

    private void UpdateStatusBadge()
    {
        bool isRunning = _processManager?.IsRunning == true;
        StatusDot.Fill = isRunning ? new SolidColorBrush(Color.Parse("#10B981")) : new SolidColorBrush(Color.Parse("#EF4444"));
        TxtStatus.Text = isRunning ? "Running" : "Stopped";
    }

    private void OnOutputLineReceived(string line)
    {
        Dispatcher.UIThread.Post(() =>
        {
            lock (_sync)
            {
                _allLogs.Add(line);
                if (_allLogs.Count > 1000)
                {
                    _allLogs.RemoveAt(0);
                }
            }

            UpdateStatusBadge();

            if (string.IsNullOrEmpty(_filterQuery) || line.Contains(_filterQuery, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(TxtLogs.Text))
                {
                    TxtLogs.Text = line;
                }
                else
                {
                    TxtLogs.Text += Environment.NewLine + line;
                }

                if (BtnAutoScroll.IsChecked == true)
                {
                    LogScrollViewer.ScrollToEnd();
                }
            }
        });
    }

    private void RenderLogs()
    {
        List<string> filtered;
        lock (_sync)
        {
            filtered = string.IsNullOrEmpty(_filterQuery)
                ? new List<string>(_allLogs)
                : _allLogs.Where(l => l.Contains(_filterQuery, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var sb = new StringBuilder();
        for (int i = 0; i < filtered.Count; i++)
        {
            if (i > 0) sb.AppendLine();
            sb.Append(filtered[i]);
        }

        TxtLogs.Text = sb.ToString();

        if (BtnAutoScroll.IsChecked == true)
        {
            LogScrollViewer.ScrollToEnd();
        }
    }
}
