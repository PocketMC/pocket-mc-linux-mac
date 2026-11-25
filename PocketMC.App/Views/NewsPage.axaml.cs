using System;
using System.ComponentModel;
using Avalonia.Controls;
using PocketMC.App.Controls;
using PocketMC.App.ViewModels;

namespace PocketMC.App.Views;

public partial class NewsPage : UserControl
{
    private NewsViewModel? _boundViewModel;

    public NewsPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    public NewsPage(NewsViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_boundViewModel != null)
        {
            _boundViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _boundViewModel = null;
        }

        if (DataContext is NewsViewModel vm)
        {
            _boundViewModel = vm;
            _boundViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        RenderSelectedArticle();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) ||
            e.PropertyName == nameof(NewsViewModel.SelectedArticle))
        {
            RenderSelectedArticle();
        }
    }

    private void RenderSelectedArticle()
    {
        if (RichArticleHost == null) return;

        RichArticleHost.Children.Clear();
        if (_boundViewModel?.SelectedArticle?.Item is { } item)
        {
            RichArticleHost.Children.Add(NewsContentRenderer.Build(item, includeTitle: false));
        }
    }
}
