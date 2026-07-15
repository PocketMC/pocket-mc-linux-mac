using System.Collections.Generic;
using System.Threading.Tasks;

namespace PocketMC.Core.Services
{
    public enum LlmProvider
    {
        OpenAi,
        Gemini,
        Claude,
        Ollama,
        Groq,
        OpenRouter,
        Mistral,
        Grok
    }

    public class DiagnosticResult
    {
        public bool Success { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
    }

    public interface IAiDiagnosticService
    {
        Task<List<LlmProvider>> GetAvailableProvidersAsync();
        Task<DiagnosticResult> AnalyzeCrashLogAsync(string slug, LlmProvider provider, string? customApiKey = null, string? modelName = null);
        Task<List<string>> GetModelsForProviderAsync(LlmProvider provider);
    }
}
