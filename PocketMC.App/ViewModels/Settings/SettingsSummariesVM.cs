using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;

namespace PocketMC.App.ViewModels.Settings
{
    public class SessionSummaryItem : ObservableObject
    {
        public string FileName { get; set; } = string.Empty;
        public string ServerName { get; set; } = string.Empty;
        public DateTime SessionStart { get; set; }
        public DateTime SessionEnd { get; set; }
        public TimeSpan Duration { get; set; }
        public string Content { get; set; } = string.Empty;
        public string AiProvider { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }

        public string FormattedDate => SessionEnd.ToLocalTime().ToString("g");
        public string FormattedDuration => Duration.TotalHours >= 1
            ? $"{(int)Duration.TotalHours}h {Duration.Minutes}m"
            : $"{Duration.Minutes}m";
    }

    public partial class SettingsSummariesVM : ObservableObject
    {
        private string _serverDir = string.Empty;
        private readonly SummaryStorageService _storageService;
        private readonly IDialogService? _dialogService;

        public ObservableCollection<SessionSummaryItem> Summaries { get; } = new();

        [ObservableProperty] private bool _isFeatureAvailable;
        [ObservableProperty] private SessionSummaryItem? _selectedSummary;
        [ObservableProperty] private string _summaryContent = string.Empty;
        [ObservableProperty] private bool _isViewingSummary;

        public bool HasSummaries => Summaries.Count > 0;
        public bool HasSelectedSummary => SelectedSummary != null;

        public IRelayCommand<SessionSummaryItem> ViewSummaryCommand { get; }
        public IAsyncRelayCommand<SessionSummaryItem> DeleteSummaryCommand { get; }
        public IRelayCommand CloseSummaryCommand { get; }

        public SettingsSummariesVM(
            string serverDir,
            SummaryStorageService? storageService = null,
            IDialogService? dialogService = null)
        {
            _serverDir = serverDir;
            _storageService = storageService ?? new SummaryStorageService();
            _dialogService = dialogService;

            ViewSummaryCommand = new RelayCommand<SessionSummaryItem>(ViewSummary);
            DeleteSummaryCommand = new AsyncRelayCommand<SessionSummaryItem>(DeleteSummaryAsync);
            CloseSummaryCommand = new RelayCommand(CloseSummary);
        }

        public void UpdateServerDir(string newDir)
        {
            _serverDir = newDir;
            Load(IsFeatureAvailable);
        }

        public void Load(bool hasApiKey)
        {
            IsFeatureAvailable = hasApiKey;
            Summaries.Clear();

            if (!hasApiKey || string.IsNullOrEmpty(_serverDir))
            {
                OnPropertyChanged(nameof(HasSummaries));
                return;
            }

            var items = _storageService.ListSummaries(_serverDir);
            foreach (var s in items)
            {
                Summaries.Add(new SessionSummaryItem
                {
                    FileName = s.FileName,
                    ServerName = s.ServerName,
                    SessionStart = s.SessionStart,
                    SessionEnd = s.SessionEnd,
                    Duration = s.Duration,
                    Content = s.Content,
                    AiProvider = s.AiProvider,
                    GeneratedAt = s.GeneratedAt
                });
            }

            OnPropertyChanged(nameof(HasSummaries));
        }

        public void ViewSummary(SessionSummaryItem? item)
        {
            if (item == null) return;
            SelectedSummary = item;
            SummaryContent = item.Content;
            IsViewingSummary = true;
        }

        public void CloseSummary()
        {
            IsViewingSummary = false;
            SummaryContent = string.Empty;
        }

        public async Task DeleteSummaryAsync(SessionSummaryItem? item)
        {
            if (item == null) return;

            if (_dialogService != null)
            {
                var confirm = await _dialogService.ShowDialogAsync(
                    "Delete Summary",
                    $"Delete summary from {item.FormattedDate}?",
                    DialogType.Question,
                    showCancel: true);

                if (confirm != DialogResult.Ok && confirm != DialogResult.Yes) return;
            }

            if (_storageService.Delete(_serverDir, item.FileName))
            {
                Summaries.Remove(item);
                if (SelectedSummary == item)
                {
                    CloseSummary();
                }
                OnPropertyChanged(nameof(HasSummaries));
            }
        }

        partial void OnSelectedSummaryChanged(SessionSummaryItem? value)
        {
            OnPropertyChanged(nameof(HasSelectedSummary));
        }
    }
}
