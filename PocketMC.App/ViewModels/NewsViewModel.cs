using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PocketMC.App.Navigation;
using PocketMC.Infrastructure.News;

namespace PocketMC.App.ViewModels
{
    public sealed class NewsItemViewModel : ObservableObject
    {
        public NewsItem Item { get; }

        public string Id => Item.Metadata.Id;
        public string Title => Item.Title;
        public string TypeLabel => Item.Metadata.Type.ToString();
        public string PriorityLabel => Item.Metadata.Priority.ToString();
        public string PublishedLabel => Item.Metadata.PublishedUtc.ToString("MMM dd, yyyy");
        public IReadOnlyList<NewsContentBlock> Blocks => Item.Blocks;

        private bool _isRead;
        public bool IsRead
        {
            get => _isRead;
            set
            {
                if (SetProperty(ref _isRead, value))
                {
                    OnPropertyChanged(nameof(UnreadVisibility));
                }
            }
        }

        public bool UnreadVisibility => !IsRead;

        public NewsItemViewModel(NewsItem item)
        {
            Item = item;
            _isRead = item.Metadata.IsRead;
        }
    }

    public partial class NewsViewModel : ObservableObject
    {
        private readonly NewsService _newsService;
        private readonly IAppNavigationService? _navigationService;

        [ObservableProperty]
        private ObservableCollection<NewsItemViewModel> _articles = new();

        [ObservableProperty]
        private NewsItemViewModel? _selectedArticle;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _syncStatusText = string.Empty;

        public bool HasArticles => Articles.Count > 0;
        public bool HasSelectedArticle => SelectedArticle != null;

        public IAsyncRelayCommand RefreshCommand { get; }
        public IRelayCommand GoBackCommand { get; }

        public NewsViewModel(NewsService newsService, IAppNavigationService? navigationService = null)
        {
            _newsService = newsService;
            _navigationService = navigationService;

            RefreshCommand = new AsyncRelayCommand(RefreshNewsAsync);
            GoBackCommand = new RelayCommand(GoBack);

            _newsService.NewsStateChanged += OnNewsStateChanged;

            LoadCachedArticles();
            _ = RefreshNewsAsync();
        }

        partial void OnSelectedArticleChanged(NewsItemViewModel? value)
        {
            OnPropertyChanged(nameof(HasSelectedArticle));
            if (value != null && !value.IsRead)
            {
                value.IsRead = true;
                _newsService.MarkRead(value.Id);
            }
        }

        private void OnNewsStateChanged()
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                LoadCachedArticles();
            });
        }

        private void LoadCachedArticles()
        {
            var cached = _newsService.GetCachedNews();
            Articles.Clear();
            foreach (var item in cached)
            {
                Articles.Add(new NewsItemViewModel(item));
            }

            if (SelectedArticle == null && Articles.Count > 0)
            {
                SelectedArticle = Articles[0];
            }

            OnPropertyChanged(nameof(HasArticles));
            OnPropertyChanged(nameof(HasSelectedArticle));
        }

        private async Task RefreshNewsAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            SyncStatusText = "Checking for announcements...";

            try
            {
                var result = await _newsService.SyncAsync();
                LoadCachedArticles();
                SyncStatusText = result.Succeeded
                    ? $"Last checked: {DateTime.Now:t}"
                    : $"Check failed: {result.ErrorMessage ?? "Network error"}";
            }
            catch (Exception ex)
            {
                SyncStatusText = $"Check failed: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void GoBack()
        {
            if (_navigationService?.CanNavigateBack == true)
            {
                _navigationService.NavigateBack();
            }
            else
            {
                _navigationService?.NavigateToDashboard();
            }
        }
    }
}
