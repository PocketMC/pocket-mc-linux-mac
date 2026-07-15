using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PocketMC.Core.Models;
using PocketMC.Core.Services;

namespace PocketMC.Infrastructure.Services
{
    public class AiDiagnosticService : IAiDiagnosticService
    {
        private readonly IInstanceService _instanceService;
        private readonly ILogRedactionService _logRedactionService;
        private readonly LlmProviderFactory _llmProviderFactory;

        private const string SystemPrompt =
            "You are an expert Minecraft server administrator and software engineer.\n" +
            "Analyze the provided crash log or error tail for a Minecraft/PocketMine server.\n" +
            "Diagnose the root cause of the crash or error.\n" +
            "Provide clear, actionable troubleshooting steps to resolve the issue.\n" +
            "Format your response in clean, beautiful Markdown with clear headings, bullet points, and code blocks. Keep it concise.";

        public AiDiagnosticService(
            IInstanceService instanceService,
            ILogRedactionService logRedactionService,
            LlmProviderFactory llmProviderFactory)
        {
            _instanceService = instanceService;
            _logRedactionService = logRedactionService;
            _llmProviderFactory = llmProviderFactory;
        }

        public async Task<List<LlmProvider>> GetAvailableProvidersAsync()
        {
            return await _llmProviderFactory.GetAvailableProvidersAsync();
        }

        public async Task<List<string>> GetModelsForProviderAsync(LlmProvider provider)
        {
            return await _llmProviderFactory.GetModelsForProviderAsync(provider);
        }

        public async Task<DiagnosticResult> AnalyzeCrashLogAsync(
            string slug,
            LlmProvider provider,
            string? customApiKey = null,
            string? modelName = null)
        {
            try
            {
                // 1. Resolve Server Instance
                var instances = await _instanceService.ListInstancesAsync();
                var instance = instances.FirstOrDefault(i => i.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
                if (instance == null)
                {
                    return new DiagnosticResult
                    {
                        Success = false,
                        Content = string.Empty,
                        ErrorMessage = $"Server instance with slug '{slug}' not found."
                    };
                }

                // 2. Fetch and Redact log tail
                var redactedLog = _logRedactionService.GetRedactedDiagnosticsLog(slug);
                if (string.IsNullOrWhiteSpace(redactedLog) || redactedLog == "No log lines available.")
                {
                    return new DiagnosticResult
                    {
                        Success = false,
                        Content = string.Empty,
                        ErrorMessage = "No server log records are available to analyze."
                    };
                }

                // 3. Assemble User Prompt
                var userPrompt = BuildUserPrompt(instance, redactedLog);

                // 4. Send request to LLM provider
                return await _llmProviderFactory.SendRequestAsync(
                    provider,
                    SystemPrompt,
                    userPrompt,
                    customApiKey,
                    modelName);
            }
            catch (Exception ex)
            {
                return new DiagnosticResult
                {
                    Success = false,
                    Content = string.Empty,
                    ErrorMessage = $"An error occurred during log ingestion and diagnostics: {ex.Message}"
                };
            }
        }

        private string BuildUserPrompt(ServerInstance instance, string redactedLogs)
        {
            return $"Server Name: {instance.Name}\n" +
                   $"Server Engine: {instance.EngineType}\n" +
                   $"Server Version: {instance.EngineVersion}\n\n" +
                   $"Tail Logs:\n" +
                   $"```\n{redactedLogs}\n```";
        }
    }
}
