using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PocketMC.Core.Models;
using PocketMC.Infrastructure.Configuration;

namespace PocketMC.Infrastructure.Services;

public sealed class PlayitStatusService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PlayitStatusService> _logger;

    public PlayitStatusService(HttpClient httpClient, ILogger<PlayitStatusService>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger ?? NullLogger<PlayitStatusService>.Instance;
    }

    public async Task<List<PlayitStatusMonitor>> GetNetworkStatusAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<PlayitStatusMonitor>();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, AppConfig.ProviderPlayitStatus);
            request.Headers.Add("User-Agent", $"{AppConfig.AppName}-Desktop/{AppConfig.AppVersion}");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var statusResponse = await response.Content.ReadFromJsonAsync<PlayitStatusResponse>(cancellationToken: cancellationToken);
            if (statusResponse?.Networks != null && statusResponse.Networks.Count > 0)
            {
                foreach (var net in statusResponse.Networks)
                {
                    if (string.IsNullOrWhiteSpace(net.Name)) continue;

                    string rawStatus = (net.Status ?? "online").ToLowerInvariant();
                    string statusClass = rawStatus switch
                    {
                        "online" => "success",
                        "re-routed" or "rerouted" or "degraded" => "warning",
                        "offline" or "down" or "outage" => "danger",
                        _ => "unknown"
                    };

                    string statusText = rawStatus switch
                    {
                        "online" => "Operational",
                        "re-routed" or "rerouted" => "Re-routed",
                        "degraded" => "Degraded",
                        "offline" or "down" or "outage" => "Outage",
                        _ => "Unknown"
                    };

                    results.Add(new PlayitStatusMonitor
                    {
                        Name = net.Name,
                        StatusClass = statusClass,
                        StatusText = statusText
                    });
                }
            }
            else if (statusResponse?.Data != null)
            {
                results.AddRange(statusResponse.Data);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch Playit network status.");
        }

        return results;
    }
}
