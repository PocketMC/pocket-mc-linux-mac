using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PocketMC.Infrastructure.Configuration;

/// <summary>
/// Single source of truth for application metadata, version/channel identity, community links,
/// proxy lists, and external API endpoints loaded from pocketmc.yml.
/// </summary>
public static class AppConfig
{
    private static readonly string[] ConfigResourceNames =
    {
        "PocketMC.Infrastructure.pocketmc.yml",
        "PocketMC.App.pocketmc.yml",
        "PocketMC.Desktop.pocketmc.yml"
    };

    public const string RemoteConfigUrl = "https://api.github.com/repos/PocketMC/pocket-mc-linux-mac/contents/pocketmc.yml?ref=main";

    private static readonly object ConfigLock = new();

    public static IReadOnlyList<string> AuthProxies { get; private set; } = new List<string>
    {
        "https://pocket-mc-proxy-20d5.onrender.com",
        "https://pocket-mc-proxy-n2qx.onrender.com"
    };

    public static IReadOnlyList<string> TelemetryProxies { get; private set; } = new List<string>
    {
        "https://pocket-mc-proxy-3fqm.onrender.com/",
        "https://pocket-mc-proxy.onrender.com/"
    };

    public static IReadOnlyList<string> DiscordApiUrls { get; private set; } = new List<string>();

    public static string AppName { get; private set; } = "PocketMC";
    public static string AppTitle { get; private set; } = "PocketMC";
    public static string AppId { get; private set; } = "PocketMC";
    public static string AppVersion { get; private set; } = "1.0.4.0-beta.1";
    public static string Channel { get; private set; } = "release";
    public static string LinkDiscord { get; private set; } = "https://discord.gg/mWdMr8Mc2m";
    public static string LinkInstagram { get; private set; } = "https://www.instagram.com/thepocketmc";
    public static string LinkFeedback { get; private set; } = "https://docs.google.com/forms/d/e/1FAIpQLSd6cNMawAbvoELxqIF_FobaC3DptKnjQxViDh9XLcyJdNbTAQ/viewform?usp=dialog";
    public static string LinkYouTube { get; private set; } = "https://www.youtube.com/@OfficialPocketMC";
    public static string LinkReddit { get; private set; } = "https://www.reddit.com/r/PocketMC/";
    public static string LinkGitHub { get; private set; } = "https://github.com/PocketMC/pocket-mc-linux-mac";
    public static string LinkReleases { get; private set; } = "https://github.com/PocketMC/pocket-mc-linux-mac/releases";
    public static string LinkWebsite { get; private set; } = "https://pocketmc.github.io/pocket-mc-website/";
    public static string LinkDocs { get; private set; } = "https://github.com/PocketMC/pocket-mc-linux-mac";
    public static string LinkDonation { get; private set; } = "https://buymeacoffee.com/sahaj33";
    public static string LinkContributorDivy { get; private set; } = "https://github.com/divyviradiya2";
    public static string LinkContributorSahaj { get; private set; } = "https://github.com/SizWinz";
    public static string LinkHelperJohndinglesin { get; private set; } = "https://github.com/Johndinglesin";
    public static string LinkDonatorNexVice { get; private set; } = "https://discord.com/users/524387761541742592";
    public static string LinkOrganization { get; private set; } = "https://ds-labs-portfolio.vercel.app";
    public static string LinkPlayitWebsite { get; private set; } = "https://playit.gg";
    public static string LinkPlayitSetup { get; private set; } = "https://playit.gg/l/setup-third-party";
    public static string LinkPlayitAgents { get; private set; } = "https://playit.gg/account/agents";
    public static string OrganizationName { get; private set; } = "DS Labs";
    public static string OrganizationTagline { get; private set; } = "Selective Digital Studio - Building Software That Works";
    public static string AppDescription { get; private set; } = "Local-first Minecraft server manager for Linux and macOS.";

    // Provider & 3rd-Party Service Endpoints
    public static string ProviderMojangManifest { get; private set; } = "https://launchermeta.mojang.com/mc/game/version_manifest.json";
    public static string ProviderMojangProfiles { get; private set; } = "https://api.mojang.com/users/profiles/minecraft";
    public static string ProviderPaperMcApi { get; private set; } = "https://fill.papermc.io/v3/projects/paper";
    public static string ProviderPurpurApi { get; private set; } = "https://api.purpurmc.org/v2/purpur";
    public static string ProviderFabricMeta { get; private set; } = "https://meta.fabricmc.net/v2";
    public static string ProviderForgeMeta { get; private set; } = "https://meta.prismlauncher.org/v1/net.minecraftforge/index.json";
    public static string ProviderForgePromotions { get; private set; } = "https://files.minecraftforge.net/net/minecraftforge/forge/promotions_slim.json";
    public static string ProviderForgeMaven { get; private set; } = "https://maven.minecraftforge.net/net/minecraftforge/forge";
    public static string ProviderNeoForgeMeta { get; private set; } = "https://meta.prismlauncher.org/v1/net.neoforged/index.json";
    public static string ProviderNeoForgeMaven { get; private set; } = "https://maven.neoforged.net/releases/net/neoforged/neoforge";
    public static string ProviderBedrockKittizz { get; private set; } = "https://raw.githubusercontent.com/kittizz/bedrock-server-downloads/main/bedrock-server-downloads.json";
    public static string ProviderPocketmineReleases { get; private set; } = "https://api.github.com/repos/pmmp/PocketMine-MP/releases";
    public static string ProviderPumpkinReleases { get; private set; } = "https://api.github.com/repos/Pumpkin-MC/Pumpkin/releases";
    public static string ProviderPhpReleases { get; private set; } = "https://api.github.com/repos/pmmp/PHP-Binaries/releases";
    public static string ProviderAdoptiumApi { get; private set; } = "https://api.adoptium.net/v3";
    public static string ProviderModrinthApi { get; private set; } = "https://api.modrinth.com/v2";
    public static string ProviderCurseForgeApi { get; private set; } = "https://api.curseforge.com/v1";
    public static string ProviderPlayitApi { get; private set; } = "https://api.playit.gg";
    public static string ProviderPlayitStatus { get; private set; } = "https://status.playit.gg/api/status?days=1";
    public static string ProviderGeyserApi { get; private set; } = "https://download.geysermc.org/v2/projects";

    // Health Check Endpoints
    public static string HealthCheckPlayit { get; private set; } = "https://playit.gg/";
    public static string HealthCheckAdoptium { get; private set; } = "https://api.adoptium.net/v3/info/release_names?page=0&size=1";
    public static string HealthCheckModrinth { get; private set; } = "https://api.modrinth.com/";

    private static readonly Lazy<HttpClient> DefaultHttpClient = new(() =>
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            AutomaticDecompression = DecompressionMethods.All
        };
        return new HttpClient(handler);
    });

    static AppConfig()
    {
        LoadEmbeddedConfig();
        LoadCachedConfig();
    }

    public static void LoadEmbeddedConfig()
    {
        try
        {
            using var stream = OpenConfigStream();
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                ParseYamlContent(reader.ReadToEnd(), preserveLocalVersion: false);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"PocketMC AppConfig failed to load embedded configuration: {ex}");
        }
    }

    public static string GetConfigFilePath()
    {
        string baseDir;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library",
                "Application Support",
                "PocketMC");
        }
        else
        {
            baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config",
                "PocketMC");
        }

        return Path.Combine(baseDir, "pocketmc.yml");
    }

    public static string GetCacheFilePath() => GetConfigFilePath();

    public static void LoadCachedConfig() => LoadLocalConfig();

    public static void LoadLocalConfig()
    {
        try
        {
            string configPath = GetConfigFilePath();
            if (File.Exists(configPath))
            {
                string content = File.ReadAllText(configPath);
                ParseYamlContent(content, preserveLocalVersion: true);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"PocketMC AppConfig failed to load local configuration: {ex}");
        }
    }

    public static void ParseYamlContent(string content, bool preserveLocalVersion = true)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        lock (ConfigLock)
        {
            try
            {
                var authProxies = new List<string>();
                var telemetryProxies = new List<string>();
                var discordApiUrls = new List<string>();

                bool inAuth = false;
                bool inTelemetry = false;
                bool inDiscord = false;

                foreach (var rawLine in content.Split('\n'))
                {
                    var trimmed = StripInlineComment(rawLine).Trim();
                    if (string.IsNullOrWhiteSpace(trimmed))
                    {
                        continue;
                    }

                    if (trimmed.StartsWith("auth_proxies:", StringComparison.Ordinal))
                    {
                        inAuth = true;
                        inTelemetry = false;
                        inDiscord = false;
                        continue;
                    }

                    if (trimmed.StartsWith("telemetry_proxies:", StringComparison.Ordinal))
                    {
                        inTelemetry = true;
                        inAuth = false;
                        inDiscord = false;
                        continue;
                    }

                    if (trimmed.StartsWith("discord_api_urls:", StringComparison.Ordinal))
                    {
                        inDiscord = true;
                        inAuth = false;
                        inTelemetry = false;
                        continue;
                    }

                    if (trimmed.StartsWith("-", StringComparison.Ordinal) && (inAuth || inTelemetry || inDiscord))
                    {
                        var match = Regex.Match(trimmed, @"^-\s*""?([^""\r\n]+)""?$");
                        if (match.Success)
                        {
                            var url = match.Groups[1].Value.Trim();
                            if (!string.IsNullOrEmpty(url))
                            {
                                if (inAuth) authProxies.Add(url);
                                if (inTelemetry) telemetryProxies.Add(url);
                                if (inDiscord) discordApiUrls.Add(url);
                            }
                        }
                        continue;
                    }

                    inAuth = false;
                    inTelemetry = false;
                    inDiscord = false;

                    if (!preserveLocalVersion)
                    {
                        if (TryMatchScalar(trimmed, "version", out var version)) AppVersion = version;
                        if (TryMatchScalar(trimmed, "channel", out var channel)) Channel = channel;
                        if (TryMatchScalar(trimmed, "app_name", out var appName)) AppName = appName;
                        if (TryMatchScalar(trimmed, "app_title", out var appTitle)) AppTitle = appTitle;
                        if (TryMatchScalar(trimmed, "app_id", out var appId)) AppId = appId;
                    }

                    if (TryMatchScalar(trimmed, "organization_name", out var orgName)) OrganizationName = orgName;
                    if (TryMatchScalar(trimmed, "organization_tagline", out var orgTagline)) OrganizationTagline = orgTagline;
                    if (TryMatchScalar(trimmed, "app_description", out var appDesc)) AppDescription = appDesc;

                    if (TryMatchScalar(trimmed, "link_discord", out var v)) LinkDiscord = v;
                    if (TryMatchScalar(trimmed, "link_instagram", out v)) LinkInstagram = v;
                    if (TryMatchScalar(trimmed, "link_feedback", out v)) LinkFeedback = v;
                    if (TryMatchScalar(trimmed, "link_youtube", out v)) LinkYouTube = v;
                    if (TryMatchScalar(trimmed, "link_reddit", out v)) LinkReddit = v;
                    if (TryMatchScalar(trimmed, "link_github", out v)) LinkGitHub = v;
                    if (TryMatchScalar(trimmed, "link_releases", out v)) LinkReleases = v;
                    if (TryMatchScalar(trimmed, "link_website", out var websiteUrl) &&
                        !websiteUrl.Contains("ds-labs", StringComparison.OrdinalIgnoreCase))
                    {
                        LinkWebsite = websiteUrl;
                    }
                    if (TryMatchScalar(trimmed, "link_docs", out v)) LinkDocs = v;
                    if (TryMatchScalar(trimmed, "link_organization", out v)) LinkOrganization = v;
                    if (TryMatchScalar(trimmed, "link_donation", out v)) LinkDonation = v;
                    if (TryMatchScalar(trimmed, "link_contributor_divy", out v)) LinkContributorDivy = v;
                    if (TryMatchScalar(trimmed, "link_contributor_sahaj", out v)) LinkContributorSahaj = v;
                    if (TryMatchScalar(trimmed, "link_helper_johndinglesin", out v)) LinkHelperJohndinglesin = v;
                    if (TryMatchScalar(trimmed, "link_donator_nexvice", out v)) LinkDonatorNexVice = v;
                    if (TryMatchScalar(trimmed, "link_playit_website", out v)) LinkPlayitWebsite = v;
                    if (TryMatchScalar(trimmed, "link_playit_setup", out v)) LinkPlayitSetup = v;
                    if (TryMatchScalar(trimmed, "link_playit_agents", out v)) LinkPlayitAgents = v;

                    if (TryMatchScalar(trimmed, "provider_mojang_manifest", out v)) ProviderMojangManifest = v;
                    if (TryMatchScalar(trimmed, "provider_mojang_profiles", out v)) ProviderMojangProfiles = v;
                    if (TryMatchScalar(trimmed, "provider_papermc_api", out v)) ProviderPaperMcApi = v;
                    if (TryMatchScalar(trimmed, "provider_purpur_api", out v)) ProviderPurpurApi = v;
                    if (TryMatchScalar(trimmed, "provider_fabric_meta", out v)) ProviderFabricMeta = v;
                    if (TryMatchScalar(trimmed, "provider_forge_meta", out v)) ProviderForgeMeta = v;
                    if (TryMatchScalar(trimmed, "provider_forge_promotions", out v)) ProviderForgePromotions = v;
                    if (TryMatchScalar(trimmed, "provider_forge_maven", out v)) ProviderForgeMaven = v;
                    if (TryMatchScalar(trimmed, "provider_neoforge_meta", out v)) ProviderNeoForgeMeta = v;
                    if (TryMatchScalar(trimmed, "provider_neoforge_maven", out v)) ProviderNeoForgeMaven = v;
                    if (TryMatchScalar(trimmed, "provider_bedrock_kittizz", out v)) ProviderBedrockKittizz = v;
                    if (TryMatchScalar(trimmed, "provider_pocketmine_releases", out v)) ProviderPocketmineReleases = v;
                    if (TryMatchScalar(trimmed, "provider_pumpkin_releases", out v)) ProviderPumpkinReleases = v;
                    if (TryMatchScalar(trimmed, "provider_php_releases", out v)) ProviderPhpReleases = v;
                    if (TryMatchScalar(trimmed, "provider_adoptium_api", out v)) ProviderAdoptiumApi = v;
                    if (TryMatchScalar(trimmed, "provider_modrinth_api", out v)) ProviderModrinthApi = v;
                    if (TryMatchScalar(trimmed, "provider_curseforge_api", out v)) ProviderCurseForgeApi = v;
                    if (TryMatchScalar(trimmed, "provider_playit_api", out v)) ProviderPlayitApi = v;
                    if (TryMatchScalar(trimmed, "provider_playit_status", out v)) ProviderPlayitStatus = v;
                    if (TryMatchScalar(trimmed, "provider_geyser_api", out v)) ProviderGeyserApi = v;

                    if (TryMatchScalar(trimmed, "health_check_playit", out v)) HealthCheckPlayit = v;
                    if (TryMatchScalar(trimmed, "health_check_adoptium", out v)) HealthCheckAdoptium = v;
                    if (TryMatchScalar(trimmed, "health_check_modrinth", out v)) HealthCheckModrinth = v;
                }

                if (authProxies.Count > 0) AuthProxies = authProxies;
                if (telemetryProxies.Count > 0) TelemetryProxies = telemetryProxies;
                if (discordApiUrls.Count > 0) DiscordApiUrls = discordApiUrls;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PocketMC AppConfig parse error: {ex}");
            }
        }
    }

    public static async Task<bool> RefreshRemoteConfigAsync(HttpClient? httpClient = null, CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(7));

            HttpClient client = httpClient ?? DefaultHttpClient.Value;

            using var request = new HttpRequestMessage(HttpMethod.Get, RemoteConfigUrl);
            request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true };
            request.Headers.TryAddWithoutValidation("Pragma", "no-cache");
            request.Headers.TryAddWithoutValidation("If-None-Match", "\"\"");
            request.Headers.TryAddWithoutValidation("User-Agent", $"PocketMC.App/{AppVersion.Replace(" ", "_")}");
            request.Headers.TryAddWithoutValidation("Accept", "application/vnd.github.v3.raw");

            var response = await client.SendAsync(request, cts.Token).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                string yaml = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(yaml) &&
                    (yaml.Contains("link_", StringComparison.Ordinal) ||
                     yaml.Contains("provider_", StringComparison.Ordinal) ||
                     yaml.Contains("app_", StringComparison.Ordinal) ||
                     yaml.Contains("version", StringComparison.Ordinal)))
                {
                    ParseYamlContent(yaml, preserveLocalVersion: true);

                    try
                    {
                        string configPath = GetConfigFilePath();
                        string? dir = Path.GetDirectoryName(configPath);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }
                        File.WriteAllText(configPath, yaml);
                    }
                    catch (Exception configEx)
                    {
                        Debug.WriteLine($"Failed to write remote config to local file: {configEx}");
                    }

                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"PocketMC AppConfig remote refresh failed (offline fallback active): {ex.Message}");
        }

        return false;
    }

    private static bool TryMatchScalar(string trimmedLine, string key, out string value)
    {
        value = string.Empty;
        if (!trimmedLine.StartsWith(key + ":", StringComparison.Ordinal))
        {
            return false;
        }

        var rawValue = trimmedLine[(key.Length + 1)..].Trim();
        if (string.IsNullOrEmpty(rawValue))
        {
            return false;
        }

        if (rawValue.Length >= 2 && rawValue.StartsWith('"') && rawValue.EndsWith('"'))
        {
            rawValue = rawValue[1..^1];
        }

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        value = rawValue.Trim();
        return true;
    }

    private static string StripInlineComment(string rawLine)
    {
        bool inQuotes = false;
        for (int i = 0; i < rawLine.Length; i++)
        {
            char c = rawLine[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == '#' && !inQuotes)
            {
                return rawLine[..i];
            }
        }

        return rawLine;
    }

    private static Stream? OpenConfigStream()
    {
        var candidates = new List<Assembly?> { typeof(AppConfig).Assembly, Assembly.GetEntryAssembly() };
        candidates.AddRange(AppDomain.CurrentDomain.GetAssemblies());

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var assembly in candidates)
        {
            if (assembly == null || assembly.IsDynamic)
            {
                continue;
            }

            string? key = assembly.FullName;
            if (string.IsNullOrWhiteSpace(key) || !seen.Add(key))
            {
                continue;
            }

            try
            {
                foreach (var resourceName in ConfigResourceNames)
                {
                    var stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream != null)
                    {
                        return stream;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PocketMC AppConfig skipped assembly resource lookup for {assembly.FullName}: {ex}");
            }
        }

        return null;
    }
}
