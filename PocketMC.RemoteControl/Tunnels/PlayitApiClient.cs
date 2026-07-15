using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PocketMC.Core.Services;
using PocketMC.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace PocketMC.RemoteControl.Tunnels;

public enum PortProtocol
{
    Tcp = 1,
    Udp = 2,
    TcpAndUdp = 3
}

public class TunnelData
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public int Port { get; set; }
    public string PublicAddress { get; set; } = string.Empty;
    public string? NumericAddress { get; set; }
    public string? TunnelType { get; set; }
    public PortProtocol? Protocol { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool HasAgentOrigin { get; set; }
    public string? AgentId { get; set; }
    public string? LocalIp { get; set; }

    public bool HasPublicAddress => !string.IsNullOrEmpty(PublicAddress);
    public string TunnelTypeDisplay => TunnelType switch
    {
        "minecraft-java" => "Minecraft Java",
        "minecraft-bedrock" => "Minecraft Bedrock",
        "mc-simple-voice-chat" => "Simple Voice Chat",
        _ => TunnelType ?? "Unknown"
    };
    public string LimitErrorText => $"Tunnel Limit Reached for {TunnelTypeDisplay}";
}

public class TunnelListResult
{
    public bool Success { get; set; }
    public List<TunnelData> Tunnels { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public bool IsTokenInvalid { get; set; }
    public bool RequiresClaim { get; set; }
}

public class TunnelCreateResult
{
    public bool Success { get; set; }
    public string? TunnelId { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
    public bool IsTokenInvalid { get; set; }
    public bool RequiresClaim { get; set; }

    public bool RequiresPlayitPremium =>
        ErrorCode is "RequiresPlayitPremium" or "RegionRequiresPlayitPremium" or "PublicPortRequiresPlayitPremium";

    public bool IsLimitError =>
        ErrorCode is "RequiresPlayitPremium" or "RegionRequiresPlayitPremium";

    public static string MapCreateError(string? errorCode)
    {
        return errorCode switch
        {
            "RequiresPlayitPremium" => "Tunnel limit reached. Upgrade to PlayIt.gg Premium to create more tunnels.",
            "RegionRequiresPlayitPremium" => "The selected region requires a PlayIt.gg Premium account.",
            "PublicPortRequiresPlayitPremium" => "A public port requires PlayIt.gg Premium.",
            "RequiresVerifiedAccount" => "Your PlayIt.gg account must be verified before creating tunnels.",
            "AgentVersionTooOld" => "The PlayIt.gg agent is out of date. Please update it and try again.",
            "AgentNotFound" => "PlayIt.gg agent not found. Please reconnect and try again.",
            "TunnelNameIsNotAscii" => "Tunnel name contains invalid characters. Use ASCII characters only.",
            "TunnelNameTooLong" => "Tunnel name is too long. Please shorten it and try again.",
            "RegionNotSupported" => "The selected region is not supported for this tunnel type.",
            "TunnelTypeBlockedOnRegion" => "This tunnel type is not available in the selected region.",
            "GatewayAlreadyHasTunnelType" => "A tunnel of this type already exists on this gateway.",
            "InvalidTunnelConfig" => "Invalid tunnel configuration. Please check your settings and try again.",
            _ => $"Tunnel creation failed: {errorCode ?? "unknown error"}. Please try again."
        };
    }
}

public class TunnelActionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    public static TunnelActionResult Ok() => new() { Success = true };
    public static TunnelActionResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}

internal sealed class PlayitApiEnvelope<TData>
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public TData? Data { get; set; }

    [JsonPropertyName("message")]
    public JsonElement Message { get; set; }
}

internal sealed class PlayitApiTunnelListV1
{
    [JsonPropertyName("tunnels")]
    public List<PlayitAccountTunnelV1> Tunnels { get; set; } = new();
}

internal sealed class PlayitAccountTunnelV1
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("tunnel_type")]
    public string? TunnelType { get; set; }

    [JsonPropertyName("user_enabled")]
    public bool UserEnabled { get; set; } = true;

    [JsonPropertyName("connect_addresses")]
    public List<PlayitConnectAddress> ConnectAddresses { get; set; } = new();

    [JsonPropertyName("origin")]
    public PlayitTunnelOriginV1? Origin { get; set; }

    [JsonPropertyName("public_allocations")]
    public List<PlayitPublicAllocation> PublicAllocations { get; set; } = new();
}

internal sealed class PlayitTunnelOriginV1
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("details")]
    public PlayitTunnelOriginDetails? Details { get; set; }

    [JsonPropertyName("data")]
    public PlayitTunnelOriginDetails? Data { get; set; }
}

internal sealed class PlayitTunnelOriginDetails
{
    [JsonPropertyName("agent_id")]
    public string? AgentId { get; set; }

    [JsonPropertyName("config_data")]
    public PlayitAgentTunnelConfig? ConfigData { get; set; }

    [JsonPropertyName("config")]
    public PlayitAgentTunnelConfig? Config { get; set; }
}

internal sealed class PlayitAgentTunnelConfig
{
    [JsonPropertyName("fields")]
    public List<PlayitAgentTunnelField> Fields { get; set; } = new();
}

internal sealed class PlayitAgentTunnelField
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

internal sealed class PlayitConnectAddress
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public JsonElement Value { get; set; }
}

internal sealed class PlayitPublicAllocation
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("details")]
    public PlayitPortAllocationDetails? Details { get; set; }
}

internal sealed class PlayitPortAllocationDetails
{
    [JsonPropertyName("ip")]
    public string? Ip { get; set; }

    [JsonPropertyName("port")]
    public int Port { get; set; }
}

public class PlayitApiClient
{
    private const string BaseApiUrl = "https://api.playit.gg";
    private readonly HttpClient _httpClient;
    private readonly ISettingsService _settingsService;

    public PlayitApiClient(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "PocketMC-Desktop-LinuxMac");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
    }

    public PlayitPartnerConnection? GetPartnerConnection()
    {
        return _settingsService.Settings.PlayitPartnerConnection;
    }

    public string? GetAgentId()
    {
        return GetPartnerConnection()?.AgentId;
    }

    public bool HasPartnerConnection()
    {
        return !string.IsNullOrWhiteSpace(GetPartnerConnection()?.SecretKey);
    }

    public string? GetSecretKey()
    {
        return GetPartnerConnection()?.SecretKey;
    }

    public async Task<TunnelListResult> GetTunnelsAsync()
    {
        string? secretKey = GetSecretKey();
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            return new TunnelListResult
            {
                Success = false,
                ErrorMessage = "PocketMC is not connected to a Playit agent yet.",
                RequiresClaim = true
            };
        }

        try
        {
            using HttpRequestMessage request = BuildAuthorizedRequest(HttpMethod.Post, "/v1/tunnels/list", secretKey, new { });
            using HttpResponseMessage response = await _httpClient.SendAsync(request);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return new TunnelListResult
                {
                    Success = false,
                    ErrorMessage = "The saved Playit credentials were rejected.",
                    IsTokenInvalid = true
                };
            }

            response.EnsureSuccessStatusCode();
            PlayitApiEnvelope<PlayitApiTunnelListV1>? apiResponse =
                JsonSerializer.Deserialize<PlayitApiEnvelope<PlayitApiTunnelListV1>>(await response.Content.ReadAsStringAsync());

            List<TunnelData> normalizedTunnels = apiResponse?.Data?.Tunnels?
                .Select(NormalizeTunnel)
                .Where(tunnel => tunnel != null)
                .Cast<TunnelData>()
                .ToList()
                ?? new List<TunnelData>();

            if (string.IsNullOrWhiteSpace(GetAgentId()))
            {
                string? foundAgentId = normalizedTunnels
                    .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.AgentId))?.AgentId;
                if (!string.IsNullOrWhiteSpace(foundAgentId))
                {
                    SaveAgentId(foundAgentId);
                }
            }

            return new TunnelListResult
            {
                Success = true,
                Tunnels = normalizedTunnels
            };
        }
        catch (Exception ex)
        {
            return new TunnelListResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<TunnelCreateResult> CreateTunnelAsync(string tunnelName, string tunnelType, int localPort)
    {
        var payload = new
        {
            name = tunnelName,
            protocol = new { type = "tunnel-type", details = tunnelType },
            origin = BuildAgentOrigin(localPort, tunnelType),
            endpoint = new { type = "region", details = new { region = "global", port = (int?)null } },
            enabled = true
        };

        string? secretKey = GetSecretKey();
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            return new TunnelCreateResult
            {
                Success = false,
                ErrorMessage = "PocketMC is not connected to a Playit agent yet.",
                RequiresClaim = true
            };
        }

        try
        {
            using HttpRequestMessage request = BuildAuthorizedRequest(HttpMethod.Post, "/v1/tunnels/create", secretKey, payload);
            using HttpResponseMessage response = await _httpClient.SendAsync(request);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return new TunnelCreateResult
                {
                    Success = false,
                    ErrorMessage = "The saved Playit credentials were rejected.",
                    IsTokenInvalid = true
                };
            }

            string body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new TunnelCreateResult
                {
                    Success = false,
                    ErrorMessage = $"Playit API returned HTTP {(int)response.StatusCode}."
                };
            }

            using JsonDocument doc = JsonDocument.Parse(body);
            JsonElement root = doc.RootElement;

            string status = root.TryGetProperty("status", out JsonElement statusEl) ? statusEl.GetString() ?? "" : "";

            if (status == "success" && root.TryGetProperty("data", out JsonElement data))
            {
                string? createdId = data.TryGetProperty("id", out JsonElement idEl) ? idEl.GetString() : null;
                return new TunnelCreateResult { Success = true, TunnelId = createdId };
            }

            if (status == "fail" && root.TryGetProperty("data", out JsonElement failData))
            {
                string failMessage = failData.ValueKind == JsonValueKind.String
                    ? failData.GetString() ?? "Unknown error"
                    : failData.ToString();
                return new TunnelCreateResult
                {
                    Success = false,
                    ErrorMessage = TunnelCreateResult.MapCreateError(failMessage),
                    ErrorCode = failMessage
                };
            }

            return new TunnelCreateResult { Success = false, ErrorMessage = $"Unexpected API response: {body}" };
        }
        catch (Exception ex)
        {
            return new TunnelCreateResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private object BuildAgentOrigin(int localPort, string tunnelType)
    {
        string? agentId = GetAgentId();

        object[] fields;
        if (IsHttpTunnelType(tunnelType))
        {
            fields = new[]
            {
                new { name = "http_port", value = localPort.ToString() },
                new { name = "https_port", value = "443" }
            };
        }
        else
        {
            fields = new[]
            {
                new { name = "local_port", value = localPort.ToString() }
            };
        }

        return new
        {
            type = "agent",
            data = new
            {
                agent_id = agentId,
                config = new
                {
                    fields
                }
            }
        };
    }

    private static bool IsHttpTunnelType(string? tunnelType) =>
        string.Equals(tunnelType, "https", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(tunnelType, "http", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(tunnelType, "http-proxy", StringComparison.OrdinalIgnoreCase);

    private HttpRequestMessage BuildAuthorizedRequest(HttpMethod method, string relativePath, string secretKey, object payload)
    {
        HttpRequestMessage request = new(method, new Uri(new Uri(BaseApiUrl), relativePath));
        request.Headers.Authorization = new AuthenticationHeaderValue("Agent-Key", secretKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        return request;
    }

    private async Task<TunnelActionResult> PostActionAsync(string path, object payload)
    {
        string? secretKey = GetSecretKey();
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            return TunnelActionResult.Fail("PocketMC is not connected to a Playit agent.");
        }

        try
        {
            using HttpRequestMessage request = BuildAuthorizedRequest(HttpMethod.Post, path, secretKey, payload);
            using HttpResponseMessage response = await _httpClient.SendAsync(request);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return TunnelActionResult.Fail("The saved Playit credentials were rejected.");
            }

            string body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return TunnelActionResult.Fail($"Playit API returned HTTP {(int)response.StatusCode}.");
            }

            using JsonDocument doc = JsonDocument.Parse(body);
            JsonElement root = doc.RootElement;

            string status = root.TryGetProperty("status", out JsonElement statusEl) ? statusEl.GetString() ?? "" : "";

            if (status == "success")
            {
                return TunnelActionResult.Ok();
            }

            if (status == "fail" && root.TryGetProperty("data", out JsonElement failData))
            {
                string failMessage = failData.ValueKind == JsonValueKind.String
                    ? failData.GetString() ?? "Unknown error"
                    : failData.ToString();
                return TunnelActionResult.Fail(failMessage);
            }

            return TunnelActionResult.Fail($"Unexpected API response: {body}");
        }
        catch (Exception ex)
        {
            return TunnelActionResult.Fail(ex.Message);
        }
    }

    public Task<TunnelActionResult> DeleteTunnelAsync(string tunnelId)
        => PostActionAsync("/v1/tunnels/delete", new { tunnel_id = tunnelId });

    public Task<TunnelActionResult> EnableTunnelAsync(string tunnelId, bool enabled)
        => PostActionAsync("/v1/tunnels/enable", new { tunnel_id = tunnelId, enabled });

    public Task<TunnelActionResult> UpdateTunnelAsync(string tunnelId, string localIp, int? localPort, string? agentId, bool enabled)
        => PostActionAsync("/v1/tunnels/update", new { tunnel_id = tunnelId, local_ip = localIp, local_port = localPort, agent_id = agentId, enabled });

    private static TunnelData? NormalizeTunnel(PlayitAccountTunnelV1 tunnel)
    {
        int? localPort = ExtractLocalPort(tunnel.Origin);
        string? publicAddress = ExtractPublicAddress(tunnel);

        return new TunnelData
        {
            Id = tunnel.Id,
            Name = tunnel.Name,
            Port = localPort ?? 0,
            PublicAddress = publicAddress ?? string.Empty,
            NumericAddress = ExtractNumericAddress(tunnel),
            TunnelType = tunnel.TunnelType,
            Protocol = InferProtocol(tunnel.TunnelType),
            IsEnabled = tunnel.UserEnabled,
            HasAgentOrigin = tunnel.Origin?.Type == "agent",
            AgentId = ExtractAgentId(tunnel.Origin),
            LocalIp = ExtractLocalIp(tunnel.Origin)
        };
    }

    private static int? ExtractLocalPort(PlayitTunnelOriginV1? origin)
    {
        return FindPortByFieldName(origin, name => string.Equals(name, "local_port", StringComparison.OrdinalIgnoreCase))
            ?? FindPortByFieldName(origin, name => string.Equals(name, "port", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "local-port", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "localPort", StringComparison.OrdinalIgnoreCase))
            ?? FindPortByFieldName(origin, name => name.Contains("port", StringComparison.OrdinalIgnoreCase));
    }

    private static string? ExtractLocalIp(PlayitTunnelOriginV1? origin)
    {
        foreach (PlayitAgentTunnelField field in EnumerateOriginFields(origin))
        {
            if (string.Equals(field.Name, "local_ip", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(field.Name, "local_address", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(field.Name, "local-ip", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(field.Name, "localAddress", StringComparison.OrdinalIgnoreCase))
            {
                return field.Value;
            }
        }
        return null;
    }

    private static string? ExtractAgentId(PlayitTunnelOriginV1? origin)
    {
        return string.IsNullOrWhiteSpace(origin?.Details?.AgentId)
            ? origin?.Data?.AgentId
            : origin.Details.AgentId;
    }

    private static int? FindPortByFieldName(PlayitTunnelOriginV1? origin, Func<string, bool> predicate)
    {
        foreach (PlayitAgentTunnelField field in EnumerateOriginFields(origin))
        {
            if (predicate(field.Name) &&
                int.TryParse(field.Value, out int parsedPort) &&
                parsedPort is >= 1 and <= 65535)
            {
                return parsedPort;
            }
        }
        return null;
    }

    private static IEnumerable<PlayitAgentTunnelField> EnumerateOriginFields(PlayitTunnelOriginV1? origin)
    {
        if (origin == null) yield break;

        foreach (PlayitTunnelOriginDetails? container in new[] { origin.Details, origin.Data })
        {
            if (container?.ConfigData?.Fields != null)
            {
                foreach (PlayitAgentTunnelField field in container.ConfigData.Fields)
                {
                    yield return field;
                }
            }
            if (container?.Config?.Fields != null)
            {
                foreach (PlayitAgentTunnelField field in container.Config.Fields)
                {
                    yield return field;
                }
            }
        }
    }

    private static string? ExtractPublicAddress(PlayitAccountTunnelV1 tunnel)
    {
        foreach (PlayitConnectAddress address in tunnel.ConnectAddresses)
        {
            if (TryExtractDisplayAddress(address, out string? displayAddress) && !string.IsNullOrWhiteSpace(displayAddress))
            {
                return displayAddress;
            }
        }

        PlayitPortAllocationDetails? allocation = tunnel.PublicAllocations
            .FirstOrDefault(x => x.Details != null)?.Details;
        if (!string.IsNullOrWhiteSpace(allocation?.Ip))
        {
            return $"{allocation.Ip}:{allocation.Port}";
        }
        return null;
    }

    private static string? ExtractNumericAddress(PlayitAccountTunnelV1 tunnel)
    {
        PlayitPortAllocationDetails? allocation = tunnel.PublicAllocations
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Details?.Ip))?.Details;
        return allocation == null ? null : $"{allocation.Ip}:{allocation.Port}";
    }

    private static bool TryExtractDisplayAddress(PlayitConnectAddress address, out string? displayAddress)
    {
        displayAddress = null;
        JsonElement value = address.Value;

        if (value.ValueKind == JsonValueKind.String)
        {
            displayAddress = value.GetString();
            return true;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        string? host = GetStringProperty(value, "address")
            ?? GetStringProperty(value, "host")
            ?? GetStringProperty(value, "hostname")
            ?? GetStringProperty(value, "domain")
            ?? GetStringProperty(value, "ip");

        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        string? port = GetStringProperty(value, "port")
            ?? GetStringProperty(value, "default_port");

        if (string.IsNullOrWhiteSpace(port) || AddressAlreadyHasPort(host))
        {
            displayAddress = host;
            return true;
        }

        displayAddress = $"{host}:{port}";
        return true;
    }

    private static string? GetStringProperty(JsonElement value, string propertyName)
    {
        if (!value.TryGetProperty(propertyName, out JsonElement property)) return null;

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number when property.TryGetInt32(out int numericValue) => numericValue.ToString(),
            _ => null
        };
    }

    private static bool AddressAlreadyHasPort(string address)
    {
        if (string.IsNullOrWhiteSpace(address)) return false;

        if (address.StartsWith("[", StringComparison.Ordinal))
        {
            int closingBracket = address.IndexOf(']');
            return closingBracket >= 0 && closingBracket + 1 < address.Length && address[closingBracket + 1] == ':';
        }

        int lastColon = address.LastIndexOf(':');
        if (lastColon < 0 || lastColon == address.Length - 1) return false;

        if (address.IndexOf(':') != lastColon) return true;

        return int.TryParse(address[(lastColon + 1)..], out _);
    }

    private static PortProtocol? InferProtocol(string? tunnelType)
    {
        if (string.IsNullOrWhiteSpace(tunnelType)) return null;

        if (IsHttpTunnelType(tunnelType)) return PortProtocol.Tcp;

        if (tunnelType.Contains("bedrock", StringComparison.OrdinalIgnoreCase) ||
            tunnelType.Contains("simple-voice-chat", StringComparison.OrdinalIgnoreCase) ||
            tunnelType.Contains("udp", StringComparison.OrdinalIgnoreCase))
        {
            return PortProtocol.Udp;
        }

        if (tunnelType.Contains("java", StringComparison.OrdinalIgnoreCase) ||
            tunnelType.Contains("tcp", StringComparison.OrdinalIgnoreCase))
        {
            return PortProtocol.Tcp;
        }
        return null;
    }

    private void SaveAgentId(string agentId)
    {
        if (string.IsNullOrWhiteSpace(agentId)) return;

        PlayitPartnerConnection? connection = GetPartnerConnection();
        if (connection == null)
        {
            connection = new PlayitPartnerConnection { AgentId = agentId };
            _settingsService.Settings.PlayitPartnerConnection = connection;
        }
        else
        {
            if (string.Equals(connection.AgentId, agentId, StringComparison.Ordinal)) return;
            connection.AgentId = agentId;
        }

        try
        {
            _settingsService.Save();
        }
        catch { }
    }
}
