using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;
using Xunit;

namespace PocketMC.Tests
{
    public class LlmProviderFactoryTests
    {
        private class MockHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

            public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
            {
                _handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return _handler(request);
            }
        }

        private class FakeSecretStore : ISecretStore
        {
            public readonly Dictionary<string, string> Storage = new();

            public Task<string?> GetAsync(string key)
            {
                Storage.TryGetValue(key, out var val);
                return Task.FromResult(val);
            }

            public Task SetAsync(string key, string value)
            {
                Storage[key] = value;
                return Task.CompletedTask;
            }

            public Task DeleteAsync(string key)
            {
                Storage.Remove(key);
                return Task.CompletedTask;
            }
        }

        [Fact]
        [Trait("Category", "Unit")]
        public async Task GetAvailableProviders_ChecksOllamaStatus()
        {
            bool ollamaCalled = false;
            var mockHandler = new MockHttpMessageHandler(req =>
            {
                if (req.RequestUri.ToString() == "http://localhost:11434/api/tags")
                {
                    ollamaCalled = true;
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"models\":[]}")
                    });
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            });

            var client = new HttpClient(mockHandler);
            var store = new FakeSecretStore();
            var factory = new LlmProviderFactory(client, store);

            var providers = await factory.GetAvailableProvidersAsync();

            Assert.True(ollamaCalled);
            Assert.Contains(LlmProvider.Ollama, providers);
            Assert.Contains(LlmProvider.OpenAi, providers);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public async Task SendRequest_OpenAi_FormatsCorrectJson()
        {
            string requestBody = string.Empty;
            var mockHandler = new MockHttpMessageHandler(async req =>
            {
                if (req.RequestUri.ToString() == "https://api.openai.com/v1/chat/completions")
                {
                    requestBody = await req.Content.ReadAsStringAsync();
                    var responseJson = "{\"choices\":[{\"message\":{\"content\":\"OpenAI mock response\"}}]}";
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(responseJson)
                    };
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

            var client = new HttpClient(mockHandler);
            var store = new FakeSecretStore();
            await store.SetAsync("openai_api_key", "mock-openai-key");

            var factory = new LlmProviderFactory(client, store);
            var result = await factory.SendRequestAsync(LlmProvider.OpenAi, "SystemPrompt", "UserPrompt");

            Assert.True(result.Success);
            Assert.Equal("OpenAI mock response", result.Content);

            var doc = JsonDocument.Parse(requestBody);
            var model = doc.RootElement.GetProperty("model").GetString();
            Assert.Equal("gpt-4o-mini", model);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public async Task SendRequest_Gemini_FormatsCorrectJson()
        {
            string requestBody = string.Empty;
            string requestUri = string.Empty;
            var mockHandler = new MockHttpMessageHandler(async req =>
            {
                requestUri = req.RequestUri.ToString();
                if (requestUri.Contains("generativelanguage.googleapis.com"))
                {
                    requestBody = await req.Content.ReadAsStringAsync();
                    var responseJson = "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"Gemini mock response\"}]}}]}";
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(responseJson)
                    };
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

            var client = new HttpClient(mockHandler);
            var store = new FakeSecretStore();
            await store.SetAsync("gemini_api_key", "mock-gemini-key");

            var factory = new LlmProviderFactory(client, store);
            var result = await factory.SendRequestAsync(LlmProvider.Gemini, "SystemPrompt", "UserPrompt");

            Assert.True(result.Success);
            Assert.Equal("Gemini mock response", result.Content);
            Assert.Contains("key=mock-gemini-key", requestUri);

            var doc = JsonDocument.Parse(requestBody);
            var promptText = doc.RootElement.GetProperty("contents")[0]
                               .GetProperty("parts")[0]
                               .GetProperty("text").GetString();
            Assert.Equal("SystemPrompt\n\nUserPrompt", promptText);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public async Task SendRequest_Claude_FormatsCorrectJson()
        {
            string requestBody = string.Empty;
            string keyHeader = string.Empty;
            var mockHandler = new MockHttpMessageHandler(async req =>
            {
                if (req.RequestUri.ToString() == "https://api.anthropic.com/v1/messages")
                {
                    requestBody = await req.Content.ReadAsStringAsync();
                    if (req.Headers.TryGetValues("x-api-key", out var values))
                    {
                        keyHeader = string.Join(",", values);
                    }
                    var responseJson = "{\"content\":[{\"text\":\"Claude mock response\"}]}";
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(responseJson)
                    };
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

            var client = new HttpClient(mockHandler);
            var store = new FakeSecretStore();
            await store.SetAsync("claude_api_key", "mock-claude-key");

            var factory = new LlmProviderFactory(client, store);
            var result = await factory.SendRequestAsync(LlmProvider.Claude, "SystemPrompt", "UserPrompt");

            Assert.True(result.Success);
            Assert.Equal("Claude mock response", result.Content);
            Assert.Equal("mock-claude-key", keyHeader);

            var doc = JsonDocument.Parse(requestBody);
            var model = doc.RootElement.GetProperty("model").GetString();
            Assert.Equal("claude-3-5-sonnet-20240620", model);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public async Task SendRequest_Ollama_FormatsCorrectJson()
        {
            string requestBody = string.Empty;
            var mockHandler = new MockHttpMessageHandler(async req =>
            {
                if (req.RequestUri.ToString() == "http://localhost:11434/api/generate")
                {
                    requestBody = await req.Content.ReadAsStringAsync();
                    var responseJson = "{\"response\":\"Ollama mock response\"}";
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(responseJson)
                    };
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

            var client = new HttpClient(mockHandler);
            var store = new FakeSecretStore();

            var factory = new LlmProviderFactory(client, store);
            var result = await factory.SendRequestAsync(LlmProvider.Ollama, "SystemPrompt", "UserPrompt");

            Assert.True(result.Success);
            Assert.Equal("Ollama mock response", result.Content);

            var doc = JsonDocument.Parse(requestBody);
            var model = doc.RootElement.GetProperty("model").GetString();
            Assert.Equal("llama3", model);
        }
    }
}
