using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PocketMC.Core.Models;
using PocketMC.Core.Services;

namespace PocketMC.App.ViewModels
{
    public partial class AiDiagnosticViewModel : ObservableObject
    {
        private readonly IAiDiagnosticService _aiDiagnosticService;
        private readonly ISecretStore _secretStore;

        [ObservableProperty]
        private ServerInstance? _selectedInstance;

        [ObservableProperty]
        private ObservableCollection<LlmProvider> _providers = new();

        [ObservableProperty]
        private LlmProvider _selectedProvider;

        [ObservableProperty]
        private ObservableCollection<string> _models = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowInitialState))]
        [NotifyPropertyChangedFor(nameof(HasAnalysisResult))]
        private string _analysisResult = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowInitialState))]
        private bool _isLoading;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowInitialState))]
        private string? _errorMessage;

        [ObservableProperty]
        private string _customApiKey = string.Empty;

        [ObservableProperty]
        private string _customModelName = string.Empty;

        public bool ShowInitialState => !IsLoading && string.IsNullOrEmpty(AnalysisResult) && string.IsNullOrEmpty(ErrorMessage);
        public bool HasAnalysisResult => !string.IsNullOrEmpty(AnalysisResult);

        public IAsyncRelayCommand AnalyzeCommand { get; }
        public IRelayCommand GoBackCommand { get; }

        public AiDiagnosticViewModel(IAiDiagnosticService aiDiagnosticService, ISecretStore secretStore)
        {
            _aiDiagnosticService = aiDiagnosticService;
            _secretStore = secretStore;
            AnalyzeCommand = new AsyncRelayCommand(AnalyzeAsync);
            GoBackCommand = new RelayCommand(GoBack);
        }

        public void Initialize(ServerInstance instance)
        {
            SelectedInstance = instance;
            AnalysisResult = string.Empty;
            ErrorMessage = null;
            IsLoading = false;
            CustomApiKey = string.Empty;
            CustomModelName = string.Empty;

            _ = LoadProvidersAsync();
        }

        private async Task LoadProvidersAsync()
        {
            var list = await _aiDiagnosticService.GetAvailableProvidersAsync();
            Dispatcher.UIThread.Post(() =>
            {
                Providers.Clear();
                foreach (var p in list)
                {
                    Providers.Add(p);
                }

                if (Providers.Count > 0)
                {
                    SelectedProvider = Providers[0];
                }
            });
        }

        partial void OnSelectedProviderChanged(LlmProvider value)
        {
            _ = LoadModelsAsync(value);
            _ = LoadPersistedApiKeyAsync(value);
        }

        private async Task LoadPersistedApiKeyAsync(LlmProvider provider)
        {
            var secretKeyName = provider switch
            {
                LlmProvider.OpenAi => "openai_api_key",
                LlmProvider.Gemini => "gemini_api_key",
                LlmProvider.Claude => "claude_api_key",
                LlmProvider.Groq => "groq_api_key",
                LlmProvider.OpenRouter => "openrouter_api_key",
                LlmProvider.Mistral => "mistral_api_key",
                LlmProvider.Grok => "grok_api_key",
                _ => null
            };

            string? key = null;
            if (secretKeyName != null)
            {
                key = await _secretStore.GetAsync(secretKeyName);
            }

            Dispatcher.UIThread.Post(() =>
            {
                CustomApiKey = key ?? string.Empty;
            });
        }

        private async Task LoadModelsAsync(LlmProvider provider)
        {
            var list = await _aiDiagnosticService.GetModelsForProviderAsync(provider);
            Dispatcher.UIThread.Post(() =>
            {
                Models.Clear();
                foreach (var m in list)
                {
                    Models.Add(m);
                }
                if (Models.Count > 0)
                {
                    CustomModelName = Models[0];
                }
                else
                {
                    CustomModelName = string.Empty;
                }
            });
        }

        private async Task AnalyzeAsync()
        {
            if (SelectedInstance == null) return;

            IsLoading = true;
            ErrorMessage = null;
            AnalysisResult = string.Empty;

            try
            {
                var apiKey = string.IsNullOrWhiteSpace(CustomApiKey) ? null : CustomApiKey;
                var model = string.IsNullOrWhiteSpace(CustomModelName) ? null : CustomModelName;

                // Persist the key securely if entered, or delete if cleared
                var secretKeyName = SelectedProvider switch
                {
                    LlmProvider.OpenAi => "openai_api_key",
                    LlmProvider.Gemini => "gemini_api_key",
                    LlmProvider.Claude => "claude_api_key",
                    LlmProvider.Groq => "groq_api_key",
                    LlmProvider.OpenRouter => "openrouter_api_key",
                    LlmProvider.Mistral => "mistral_api_key",
                    LlmProvider.Grok => "grok_api_key",
                    _ => null
                };

                if (secretKeyName != null)
                {
                    if (string.IsNullOrWhiteSpace(CustomApiKey))
                    {
                        await _secretStore.DeleteAsync(secretKeyName);
                    }
                    else
                    {
                        await _secretStore.SetAsync(secretKeyName, CustomApiKey);
                    }
                }

                var result = await _aiDiagnosticService.AnalyzeCrashLogAsync(
                    SelectedInstance.Slug,
                    SelectedProvider,
                    apiKey,
                    model);

                Dispatcher.UIThread.Post(() =>
                {
                    if (result.Success)
                    {
                        AnalysisResult = result.Content;
                    }
                    else
                    {
                        ErrorMessage = result.ErrorMessage;
                    }
                });
            }
            catch (Exception ex)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    ErrorMessage = $"An error occurred during diagnostic execution: {ex.Message}";
                });
            }
            finally
            {
                Dispatcher.UIThread.Post(() =>
                {
                    IsLoading = false;
                });
            }
        }

        private void GoBack()
        {
            if (SelectedInstance == null) return;
            var mainVM = App.Services.GetService(typeof(MainWindowViewModel)) as MainWindowViewModel;
            if (mainVM != null)
            {
                var consoleVM = App.Services.GetRequiredService<ServerConsoleViewModel>();
                consoleVM.Initialize(SelectedInstance);
                mainVM.CurrentViewModel = consoleVM;
            }
        }
    }
}
