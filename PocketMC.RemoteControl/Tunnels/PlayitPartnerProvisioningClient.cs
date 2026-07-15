using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace PocketMC.RemoteControl.Tunnels;

public sealed class PlayitPartnerAgentVersion
{
    [JsonPropertyName("versionMajor")]
    public int VersionMajor { get; set; } = 0;

    [JsonPropertyName("versionMinor")]
    public int VersionMinor { get; set; } = 17;

    [JsonPropertyName("versionPatch")]
    public int VersionPatch { get; set; } = 1;

    public override string ToString() => $"{VersionMajor}.{VersionMinor}.{VersionPatch}";
}

public sealed class PlayitPartnerCreateAgentRequest
{
    [JsonPropertyName("setupCode")]
    public string SetupCode { get; set; } = string.Empty;

    [JsonPropertyName("agentName")]
    public string AgentName { get; set; } = "PocketMC-LinuxMac";

    [JsonPropertyName("platform")]
    public string Platform { get; set; } = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX) ? "macos" : "linux";

    [JsonPropertyName("agentVersion")]
    public PlayitPartnerAgentVersion AgentVersion { get; set; } = new();
}

public sealed class PlayitPartnerCreateAgentResponse
{
    [JsonPropertyName("accountId")]
    public long? AccountId { get; set; }

    [JsonPropertyName("agentId")]
    public string AgentId { get; set; } = string.Empty;

    [JsonPropertyName("agentSecretKey")]
    public string AgentSecretKey { get; set; } = string.Empty;

    [JsonPropertyName("agentOverLimit")]
    public bool AgentOverLimit { get; set; }

    [JsonPropertyName("connectedEmail")]
    public string? ConnectedEmail { get; set; }
}

public sealed class PlayitPartnerCreateAgentResult
{
    public bool Success { get; set; }
    public PlayitPartnerCreateAgentResponse? Response { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class PlayitPartnerProvisioningClient
{
    private readonly HttpClient _httpClient;
    private readonly List<string> _backendUrls = new()
    {
        "https://pocket-mc-proxy-20d5.onrender.com",
        "https://pocket-mc-proxy-n2qx.onrender.com"
    };

    public PlayitPartnerProvisioningClient()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "PocketMC-Desktop-LinuxMac");
    }

    public Uri GetSetupPageUri()
    {
        return new Uri("https://playit.gg/l/setup-third-party");
    }

    public async Task<PlayitPartnerCreateAgentResult> CreateAgentAsync(string setupCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(setupCode))
        {
            return new PlayitPartnerCreateAgentResult
            {
                Success = false,
                ErrorMessage = "Enter a Playit setup code first."
            };
        }

        var request = new PlayitPartnerCreateAgentRequest
        {
            SetupCode = setupCode.Trim()
        };

        Exception? lastException = null;
        foreach (var baseUrl in _backendUrls)
        {
            try
            {
                var endpoint = new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), "api/playit/partner/create-agent");
                var response = await _httpClient.PostAsJsonAsync(endpoint, request, cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var payload = await response.Content.ReadFromJsonAsync<PlayitPartnerCreateAgentResponse>(cancellationToken: cancellationToken);
                    if (payload != null && !string.IsNullOrWhiteSpace(payload.AgentSecretKey))
                    {
                        return new PlayitPartnerCreateAgentResult
                        {
                            Success = true,
                            Response = payload
                        };
                    }
                }
                else
                {
                    string body = await response.Content.ReadAsStringAsync(cancellationToken);
                    lastException = new Exception(string.IsNullOrWhiteSpace(body) ? $"API returned {(int)response.StatusCode}" : body);
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
            }
        }

        return new PlayitPartnerCreateAgentResult
        {
            Success = false,
            ErrorMessage = lastException?.Message ?? "Failed to connect to Playit partner backend."
        };
    }
}
