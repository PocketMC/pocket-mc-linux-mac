using System;
using System.Diagnostics;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PocketMC.Infrastructure.WhatsNew;

namespace PocketMC.App.Views
{
    public partial class WhatsNewWindow : Window
    {
        private readonly WhatsNewService? _whatsNewService;

        public WhatsNewWindow()
        {
            InitializeComponent();
        }

        public WhatsNewWindow(WhatsNewService whatsNewService) : this()
        {
            _whatsNewService = whatsNewService;

            string appDir = AppContext.BaseDirectory;
            string changelogPath = Path.Combine(appDir, "Assets", "WhatsNew.txt");
            string? content = null;
            if (File.Exists(changelogPath))
            {
                content = File.ReadAllText(changelogPath);
            }

            var entry = _whatsNewService.LoadChangelog(content);
            if (entry != null)
            {
                TxtHeader.Text = $"What's New in v{entry.Version}";
                TxtSubheader.Text = $"PocketMC Desktop - Highlights and improvements for version {entry.Version}";
                SectionsList.ItemsSource = entry.Sections;
            }
        }

        private void OnGotIt_Click(object? sender, RoutedEventArgs e)
        {
            _whatsNewService?.MarkAsSeen();
            Close();
        }

        private void OnViewFullChangelog_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "https://github.com/PocketMC/pocket-mc-linux-mac/releases",
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch { }
        }
    }
}
