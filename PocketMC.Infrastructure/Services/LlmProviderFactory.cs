using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using PocketMC.Core.Services;

namespace PocketMC.Infrastructure.Services
{
    public class LlmProviderFactory
    {
        private readonly HttpClient _httpClient;
        private readonly ISecretStore _secretStore;

        public LlmProviderFactory(HttpClient httpClient, ISecretStore secretStore)
        {
            _httpClient = httpClient;
            _secretStore = secretStore;
        }

        public async Task<bool> IsOllamaRunningAsync()
        {
            try
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(1.5));
                var response = await _httpClient.GetAsync("http://localhost:11434/api/tags", cts.Token);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<LlmProvider>> GetAvailableProvidersAsync()
        {
            var list = new List<LlmProvider>
            {
                LlmProvider.OpenAi,
                LlmProvider.Gemini,
                LlmProvider.Claude,
                LlmProvider.Groq,
                LlmProvider.OpenRouter,
                LlmProvider.Mistral,
                LlmProvider.Grok
            };

            if (await IsOllamaRunningAsync())
            {
                list.Add(LlmProvider.Ollama);
            }

            return list;
        }

        public async Task<DiagnosticResult> SendRequestAsync(
            LlmProvider provider,
            string systemPrompt,
            string userPrompt,
            string? customApiKey = null,
            string? customModelName = null)
        {
            // 1. Resolve API key if needed
            string? apiKey = customApiKey;
            if (string.IsNullOrEmpty(apiKey) && provider != LlmProvider.Ollama)
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
                    _ => throw new ArgumentOutOfRangeException()
                };
                apiKey = await _secretStore.GetAsync(secretKeyName);
            }

            if (string.IsNullOrEmpty(apiKey) && provider != LlmProvider.Ollama)
            {
                return new DiagnosticResult
                {
                    Success = false,
                    Content = string.Empty,
                    ErrorMessage = $"API key for {provider} is not configured. Please configure it in Server Settings or supply a custom key."
                };
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "");
                string jsonPayload = "";

                switch (provider)
                {
                    case LlmProvider.OpenAi:
                        {
                            var model = customModelName ?? "gpt-4o-mini";
                            request.RequestUri = new Uri("https://api.openai.com/v1/chat/completions");
                            request.Headers.Add("Authorization", $"Bearer {apiKey}");

                            var payload = new
                            {
                                model = model,
                                messages = new[]
                                {
                                    new { role = "system", content = systemPrompt },
                                    new { role = "user", content = userPrompt }
                                }
                            };
                            jsonPayload = JsonSerializer.Serialize(payload);
                            break;
                        }
                    case LlmProvider.Gemini:
                        {
                            var model = customModelName ?? "gemini-1.5-flash";
                            request.RequestUri = new Uri($"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}");

                            var payload = new
                            {
                                contents = new[]
                                {
                                    new
                                    {
                                        parts = new[]
                                        {
                                            new { text = $"{systemPrompt}\n\n{userPrompt}" }
                                        }
                                    }
                                }
                            };
                            jsonPayload = JsonSerializer.Serialize(payload);
                            break;
                        }
                    case LlmProvider.Claude:
                        {
                            var model = customModelName ?? "claude-3-5-sonnet-20240620";
                            request.RequestUri = new Uri("https://api.anthropic.com/v1/messages");
                            request.Headers.Add("x-api-key", apiKey);
                            request.Headers.Add("anthropic-version", "2023-06-01");

                            var payload = new
                            {
                                model = model,
                                max_tokens = 2048,
                                system = systemPrompt,
                                messages = new[]
                                {
                                    new { role = "user", content = userPrompt }
                                }
                            };
                            jsonPayload = JsonSerializer.Serialize(payload);
                            break;
                        }
                    case LlmProvider.Ollama:
                        {
                            var model = customModelName ?? "llama3";
                            request.RequestUri = new Uri("http://localhost:11434/api/generate");

                            var payload = new
                            {
                                model = model,
                                system = systemPrompt,
                                prompt = userPrompt,
                                stream = false
                            };
                            jsonPayload = JsonSerializer.Serialize(payload);
                            break;
                        }
                    case LlmProvider.Groq:
                    case LlmProvider.OpenRouter:
                    case LlmProvider.Mistral:
                    case LlmProvider.Grok:
                        {
                            var (url, defaultModel) = provider switch
                            {
                                LlmProvider.Groq => ("https://api.groq.com/openai/v1/chat/completions", "llama-3.1-70b-versatile"),
                                LlmProvider.OpenRouter => ("https://openrouter.ai/api/v1/chat/completions", "meta-llama/llama-3.1-8b-instruct:free"),
                                LlmProvider.Mistral => ("https://api.mistral.ai/v1/chat/completions", "mistral-large-latest"),
                                LlmProvider.Grok => ("https://api.x.ai/v1/chat/completions", "grok-beta"),
                                _ => throw new ArgumentOutOfRangeException()
                            };

                            var model = customModelName ?? defaultModel;
                            request.RequestUri = new Uri(url);
                            request.Headers.Add("Authorization", $"Bearer {apiKey}");

                            if (provider == LlmProvider.OpenRouter)
                            {
                                request.Headers.Add("HTTP-Referer", "https://pocketmc.app");
                                request.Headers.Add("X-Title", "PocketMC");
                            }

                            var payload = new
                            {
                                model = model,
                                messages = new[]
                                {
                                    new { role = "system", content = systemPrompt },
                                    new { role = "user", content = userPrompt }
                                }
                            };
                            jsonPayload = JsonSerializer.Serialize(payload);
                            break;
                        }
                }

                request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                using var response = await _httpClient.SendAsync(request);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return new DiagnosticResult
                    {
                        Success = false,
                        Content = string.Empty,
                        ErrorMessage = $"LLM Provider returned error ({response.StatusCode}): {responseString}"
                    };
                }

                var doc = JsonDocument.Parse(responseString);
                string responseText = provider switch
                {
                    LlmProvider.OpenAi or LlmProvider.Groq or LlmProvider.OpenRouter or LlmProvider.Mistral or LlmProvider.Grok 
                        => doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty,
                    LlmProvider.Gemini => doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? string.Empty,
                    LlmProvider.Claude => doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? string.Empty,
                    LlmProvider.Ollama => doc.RootElement.GetProperty("response").GetString() ?? string.Empty,
                    _ => throw new ArgumentOutOfRangeException()
                };

                return new DiagnosticResult
                {
                    Success = true,
                    Content = responseText
                };
            }
            catch (Exception ex)
            {
                return new DiagnosticResult
                {
                    Success = false,
                    Content = string.Empty,
                    ErrorMessage = $"Failed to execute AI diagnostics: {ex.Message}"
                };
            }
        }

        public async Task<List<string>> GetModelsForProviderAsync(LlmProvider provider)
        {
            try
            {
                if (provider == LlmProvider.Ollama)
                {
                    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(2));
                    var response = await _httpClient.GetAsync("http://localhost:11434/api/tags", cts.Token);
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);
                        var modelsList = new List<string>();
                        if (doc.RootElement.TryGetProperty("models", out var modelsElement) && modelsElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var modelItem in modelsElement.EnumerateArray())
                            {
                                if (modelItem.TryGetProperty("name", out var nameElement))
                                {
                                    modelsList.Add(nameElement.GetString()!);
                                }
                            }
                        }
                        if (modelsList.Count > 0) return modelsList;
                    }
                    return new List<string> { "llama3", "mistral", "gemma2", "phi3" };
                }
            }
            catch
            {
                // Fallback to defaults if Ollama is not responding
            }

            return provider switch
            {
                LlmProvider.OpenAi => new List<string> { "gpt-4o-mini", "gpt-4o", "gpt-3.5-turbo" },
                LlmProvider.Gemini => new List<string> { "gemini-1.5-flash", "gemini-1.5-pro" },
                LlmProvider.Claude => new List<string> { "claude-3-5-sonnet-20240620", "claude-3-haiku-20240307", "claude-3-opus-20240229" },
                LlmProvider.Groq => new List<string> { "llama-3.1-70b-versatile", "llama-3.1-8b-instant", "mixtral-8x7b-32768", "gemma2-9b-it" },
                LlmProvider.OpenRouter => new List<string> { "meta-llama/llama-3.1-8b-instruct:free", "google/gemma-2-9b-it:free", "meta-llama/llama-3.1-70b-instruct" },
                LlmProvider.Mistral => new List<string> { "mistral-large-latest", "open-mixtral-8x22b", "codestral-latest" },
                LlmProvider.Grok => new List<string> { "grok-beta", "grok-2" },
                _ => new List<string>()
            };
        }
    }
}
