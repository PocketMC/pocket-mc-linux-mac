using System;
using System.Diagnostics;
using System.IO;
using Avalonia.Controls;
using PocketMC.Core.Models;

namespace PocketMC.App.Views.Dialogs;

public partial class ServerCrashDialogWindow : Window
{
    private readonly CrashAnalysisResult? _crashResult;
    private readonly ServerInstance? _instance;

    public bool ShouldRestart { get; private set; }

    public ServerCrashDialogWindow()
    {
        InitializeComponent();
    }

    public ServerCrashDialogWindow(ServerInstance instance, CrashAnalysisResult crashResult)
    {
        _instance = instance ?? throw new ArgumentNullException(nameof(instance));
        _crashResult = crashResult ?? throw new ArgumentNullException(nameof(crashResult));

        InitializeComponent();

        TxtServerName.Text = instance.Name;
        TxtServerDetails.Text = $"{instance.EngineType} {instance.EngineVersion}";
        TxtCategoryBadge.Text = crashResult.Category.ToString();
        TxtCrashTitle.Text = string.IsNullOrWhiteSpace(crashResult.Title) ? "Crash Detected" : crashResult.Title;
        TxtCrashSummary.Text = string.IsNullOrWhiteSpace(crashResult.Summary) ? "Server stopped unexpectedly." : crashResult.Summary;
        TxtLogViewer.Text = crashResult.FullLogContext;

        if (!string.IsNullOrEmpty(crashResult.CrashReportPath) && File.Exists(crashResult.CrashReportPath))
        {
            BtnOpenFolder.IsVisible = true;
            BtnOpenFolder.Click += (s, e) =>
            {
                try
                {
                    string dir = Path.GetDirectoryName(crashResult.CrashReportPath)!;
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = dir,
                        UseShellExecute = true
                    });
                }
                catch { }
            };
        }

        BtnCopyLogs.Click += async (s, e) =>
        {
            if (Clipboard != null && !string.IsNullOrEmpty(crashResult.FullLogContext))
            {
                await Clipboard.SetTextAsync(crashResult.FullLogContext);
            }
        };

        BtnClose.Click += (s, e) =>
        {
            ShouldRestart = false;
            Close();
        };

        BtnRestart.Click += (s, e) =>
        {
            ShouldRestart = true;
            Close();
        };
    }
}
