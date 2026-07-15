using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using Microsoft.Extensions.Logging;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services.Providers.OAuth;

namespace PocketMC.Infrastructure.Services
{
    public class GoogleDriveBackupProvider : ICloudBackupProvider
    {
        public const string ClientId = "10119503717-8rudcoou9k0iuhepsqgntk9ahuso6krc.apps.googleusercontent.com";
        private const string RedirectUri = "http://127.0.0.1:49384/callback";

        public CloudBackupProviderType ProviderType => CloudBackupProviderType.GoogleDrive;

        private readonly ISettingsService _settingsService;
        private readonly ILogger<GoogleDriveBackupProvider> _logger;
        private readonly HttpClient _httpClient;

        private static readonly IReadOnlyList<string> AuthProxies = new List<string>
        {
            "https://pocket-mc-proxy-20d5.onrender.com",
            "https://pocket-mc-proxy-n2qx.onrender.com"
        };

        public GoogleDriveBackupProvider(ISettingsService settingsService, ILogger<GoogleDriveBackupProvider> logger, HttpClient httpClient)
        {
            _settingsService = settingsService;
            _logger = logger;
            _httpClient = httpClient;
        }

        private async Task<string?> GetValidAccessTokenAsync(CancellationToken ct)
        {
            var settings = _settingsService.Settings;
            if (!settings.CloudTokens.TryGetValue("GoogleDrive", out var tokens)) return null;

            if (tokens.ExpiresAtUtc.HasValue && tokens.ExpiresAtUtc.Value <= DateTimeOffset.UtcNow.AddMinutes(5))
            {
                if (string.IsNullOrEmpty(tokens.RefreshToken)) return null;

                try
                {
                    var refreshRequest = new { refreshToken = tokens.RefreshToken };
                    var response = await PostToProxyAsync("/api/google/oauth/refresh", refreshRequest, ct);
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync(ct);
                        throw new HttpRequestException($"Google proxy refresh failed (HTTP {response.StatusCode}): {errorContent}");
                    }

                    var json = await response.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);

                    tokens.AccessToken = doc.RootElement.GetProperty("access_token").GetString();
                    int expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();
                    tokens.ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(expiresIn);

                    if (doc.RootElement.TryGetProperty("refresh_token", out var rtElement))
                    {
                        tokens.RefreshToken = rtElement.GetString() ?? tokens.RefreshToken;
                    }

                    settings.CloudTokens["GoogleDrive"] = tokens;
                    _settingsService.Save();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to refresh Google Drive token via proxy.");
                    return null;
                }
            }

            return tokens.AccessToken;
        }

        private async Task<DriveService?> GetServiceAsync(CancellationToken ct)
        {
            string? token = await GetValidAccessTokenAsync(ct);
            if (token == null) return null;

            var initializer = new GoogleProxyTokenInitializer(async () =>
            {
                string? latestToken = await GetValidAccessTokenAsync(ct);
                return latestToken ?? throw new UnauthorizedAccessException("Google Drive token is expired or missing.");
            });

            return new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = initializer,
                ApplicationName = "PocketMC-Desktop/1.0"
            });
        }

        public async Task<CloudBackupConnectionStatus> GetStatusAsync(CancellationToken ct)
        {
            var service = await GetServiceAsync(ct);
            if (service == null) return CloudBackupConnectionStatus.Disconnected;

            try
            {
                var req = service.About.Get();
                req.Fields = "user";
                await req.ExecuteAsync(ct);
                return CloudBackupConnectionStatus.Connected;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check Google Drive backup provider status.");
                return CloudBackupConnectionStatus.Expired;
            }
        }

        public async Task<CloudBackupAccount?> GetAccountAsync(CancellationToken ct)
        {
            var service = await GetServiceAsync(ct);
            if (service == null) return null;

            var status = await GetStatusAsync(ct);
            if (status != CloudBackupConnectionStatus.Connected) return null;

            var req = service.About.Get();
            req.Fields = "user";
            var about = await req.ExecuteAsync(ct);

            return new CloudBackupAccount
            {
                Provider = ProviderType,
                DisplayName = about.User?.DisplayName,
                Email = about.User?.EmailAddress,
                Status = status
            };
        }

        public async Task ConnectAsync(CancellationToken ct)
        {
            var (codeVerifier, codeChallenge) = PkceHelper.Generate();
            var state = Guid.NewGuid().ToString("N");

            string authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?client_id={ClientId}&redirect_uri={Uri.EscapeDataString(RedirectUri)}&response_type=code&scope={Uri.EscapeDataString(DriveService.Scope.DriveFile)}&access_type=offline&prompt=consent&state={state}&code_challenge={codeChallenge}&code_challenge_method=S256";

            PocketMC.Infrastructure.Services.Providers.OAuth.BrowserHelper.OpenUrl(authUrl);

            var receiver = new LoopbackOAuthReceiver();
            var (code, error) = await receiver.ReceiveCodeAsync(RedirectUri + "/", ct, state);

            if (!string.IsNullOrEmpty(error)) throw new Exception($"Google Auth Error: {error}");
            if (string.IsNullOrEmpty(code)) throw new Exception("No code returned.");

            var exchangeRequest = new
            {
                code = code,
                redirectUri = RedirectUri,
                codeVerifier = codeVerifier
            };

            var response = await PostToProxyAsync("/api/google/oauth/token", exchangeRequest, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException($"Google proxy exchange failed (HTTP {response.StatusCode}): {errorContent}");
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            string accessToken = doc.RootElement.GetProperty("access_token").GetString()!;
            string refreshToken = doc.RootElement.TryGetProperty("refresh_token", out var rt) ? rt.GetString()! : "";
            int expiresIn = doc.RootElement.TryGetProperty("expires_in", out var ei) ? ei.GetInt32() : 0;

            var settings = _settingsService.Settings;
            settings.CloudTokens["GoogleDrive"] = new CloudOAuthTokenSet
            {
                Provider = ProviderType,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAtUtc = expiresIn > 0 ? DateTimeOffset.UtcNow.AddSeconds(expiresIn) : null
            };
            _settingsService.Save();
        }

        public async Task DisconnectAsync(CancellationToken ct)
        {
            var settings = _settingsService.Settings;
            if (settings.CloudTokens.TryGetValue("GoogleDrive", out var tokens) && !string.IsNullOrEmpty(tokens.AccessToken))
            {
                try
                {
                    var content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("token", tokens.AccessToken) });
                    await _httpClient.PostAsync("https://oauth2.googleapis.com/revoke", content, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to revoke Google Drive token during disconnect.");
                }
            }

            settings.CloudTokens.Remove("GoogleDrive");
            _settingsService.Save();
        }

        public Task ValidateAsync(CancellationToken ct) => Task.CompletedTask;

        private class GoogleProxyTokenInitializer : Google.Apis.Http.IConfigurableHttpClientInitializer, Google.Apis.Http.IHttpExecuteInterceptor
        {
            private readonly Func<Task<string>> _getTokenAsync;

            public GoogleProxyTokenInitializer(Func<Task<string>> getTokenAsync)
            {
                _getTokenAsync = getTokenAsync;
            }

            public void Initialize(Google.Apis.Http.ConfigurableHttpClient httpClient)
            {
                httpClient.MessageHandler.AddExecuteInterceptor(this);
            }

            public async Task InterceptAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string token = await _getTokenAsync();
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }

        private static string EscapeDriveQueryStringLiteral(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            return value.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("'", "\\'", StringComparison.Ordinal);
        }

        private static string FolderNameEqualsQuery(string folderName)
        {
            return $"mimeType='application/vnd.google-apps.folder' and name='{EscapeDriveQueryStringLiteral(folderName)}' and trashed=false";
        }

        private async Task<string> GetOrCreateFolderAsync(DriveService service, string folderName, string? parentId = null)
        {
            var request = service.Files.List();
            request.Q = FolderNameEqualsQuery(folderName);
            if (parentId != null) request.Q += $" and '{EscapeDriveQueryStringLiteral(parentId)}' in parents";
            else request.Q += " and 'root' in parents";

            var result = await request.ExecuteAsync();
            if (result.Files != null && result.Files.Count > 0)
            {
                return result.Files[0].Id;
            }

            var fileMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = folderName,
                MimeType = "application/vnd.google-apps.folder"
            };
            if (parentId != null)
            {
                fileMetadata.Parents = new List<string> { parentId };
            }
            var createRequest = service.Files.Create(fileMetadata);
            createRequest.Fields = "id";
            var file = await createRequest.ExecuteAsync();
            return file.Id;
        }

        public async Task<CloudBackupUploadResult> UploadBackupAsync(CloudBackupUploadRequest request)
        {
            return await Providers.ResilientUploadPolicy.ExecuteAsync(async (cancellationToken) =>
            {
                var service = await GetServiceAsync(cancellationToken);
                if (service == null) throw new UnauthorizedAccessException("Google Drive token expired or missing.");

                string rootId = await GetOrCreateFolderAsync(service, "PocketMC Backups");
                string instanceFolderName = $"{CloudPathSanitizer.SanitizeFolderName(request.InstanceName)}-{request.InstanceId}";
                string instanceFolderId = await GetOrCreateFolderAsync(service, instanceFolderName, rootId);

                var fileMetadata = new Google.Apis.Drive.v3.Data.File()
                {
                    Name = CloudPathSanitizer.SanitizeFolderName(request.BackupFileName),
                    Parents = new List<string> { instanceFolderId }
                };

                var fileInfo = new FileInfo(request.LocalZipPath);
                using var stream = new FileStream(request.LocalZipPath, FileMode.Open, FileAccess.Read);

                var createRequest = service.Files.Create(fileMetadata, stream, "application/zip");
                createRequest.ChunkSize = ResumableUpload.MinimumChunkSize * 4;

                long totalBytes = fileInfo.Length;

                createRequest.ProgressChanged += (progress) =>
                {
                    if (progress.Status == UploadStatus.Uploading || progress.Status == UploadStatus.Completed)
                    {
                        request.Progress?.Report(new CloudBackupProgress
                        {
                            Provider = ProviderType,
                            Stage = progress.Status == UploadStatus.Completed ? "Done" : "Uploading",
                            BytesUploaded = progress.BytesSent,
                            TotalBytes = totalBytes,
                            Percent = (double)progress.BytesSent / totalBytes * 100,
                            Message = progress.Status == UploadStatus.Completed ? "Finished" : "Uploading chunks..."
                        });
                    }
                };

                var result = await createRequest.UploadAsync(cancellationToken);

                if (result.Status == UploadStatus.Failed)
                {
                    throw result.Exception ?? new Exception("Google Drive upload failed silently.");
                }

                return new CloudBackupUploadResult
                {
                    Success = true,
                    Provider = ProviderType,
                    ProviderFileId = createRequest.ResponseBody?.Id,
                    RemotePath = $"/PocketMC Backups/{instanceFolderName}/{fileMetadata.Name}",
                    BytesUploaded = totalBytes,
                    Recoverable = false
                };
            }, _logger, request.CancellationToken);
        }

        public async Task<IReadOnlyList<CloudRemoteBackupItem>> ListBackupsAsync(Guid instanceId, string instanceName, CancellationToken ct)
        {
            var service = await GetServiceAsync(ct);
            if (service == null) return Array.Empty<CloudRemoteBackupItem>();

            string instanceFolderName = $"{CloudPathSanitizer.SanitizeFolderName(instanceName)}-{instanceId}";

            var folderReq = service.Files.List();
            folderReq.Q = FolderNameEqualsQuery(instanceFolderName);
            var folderRes = await folderReq.ExecuteAsync(ct);

            if (folderRes.Files == null || folderRes.Files.Count == 0) return Array.Empty<CloudRemoteBackupItem>();

            string folderId = folderRes.Files[0].Id;

            var listReq = service.Files.List();
            listReq.Q = $"'{EscapeDriveQueryStringLiteral(folderId)}' in parents and trashed=false";
            listReq.Fields = "files(id, name, size, createdTime)";

            var listRes = await listReq.ExecuteAsync(ct);

            var results = new List<CloudRemoteBackupItem>();
            if (listRes.Files != null)
            {
                foreach (var f in listRes.Files)
                {
                    results.Add(new CloudRemoteBackupItem
                    {
                        Provider = ProviderType,
                        ProviderFileId = f.Id,
                        FileName = f.Name,
                        RemotePath = $"/PocketMC Backups/{instanceFolderName}/{f.Name}",
                        SizeBytes = f.Size ?? 0,
                        CreatedUtc = f.CreatedTimeDateTimeOffset ?? DateTimeOffset.UtcNow
                    });
                }
            }
            return results;
        }

        public async Task DeleteBackupAsync(string providerFileId, CancellationToken ct)
        {
            var service = await GetServiceAsync(ct);
            if (service == null) return;

            await service.Files.Delete(providerFileId).ExecuteAsync(ct);
        }

        public async Task DownloadBackupAsync(string providerFileId, string localDestinationPath, CancellationToken ct, IProgress<double>? progress = null)
        {
            var service = await GetServiceAsync(ct);
            if (service == null) throw new UnauthorizedAccessException("Google Drive token is expired or missing.");

            var request = service.Files.Get(providerFileId);
            
            long totalSize = 0;
            try
            {
                var metaRequest = service.Files.Get(providerFileId);
                metaRequest.Fields = "size";
                var meta = await metaRequest.ExecuteAsync(ct);
                if (meta.Size.HasValue) totalSize = meta.Size.Value;
            }
            catch { }

            request.MediaDownloader.ProgressChanged += (downloadProgress) =>
            {
                if (progress != null)
                {
                    if (downloadProgress.Status == Google.Apis.Download.DownloadStatus.Downloading)
                    {
                        if (totalSize > 0)
                        {
                            double percent = (double)downloadProgress.BytesDownloaded / totalSize * 100.0;
                            progress.Report(percent);
                        }
                    }
                }
            };

            using var stream = new FileStream(localDestinationPath, FileMode.Create, FileAccess.Write);
            await request.DownloadAsync(stream, ct);
        }

        private async Task<HttpResponseMessage> PostToProxyAsync<T>(string path, T payload, CancellationToken ct)
        {
            Exception? lastException = null;

            foreach (var url in AuthProxies)
            {
                if (string.IsNullOrWhiteSpace(url)) continue;
                
                try
                {
                    var response = await _httpClient.PostAsJsonAsync($"{url.TrimEnd('/')}{path}", payload, ct);
                    if (response.IsSuccessStatusCode)
                        return response;
                    
                    if (url == AuthProxies[AuthProxies.Count - 1]) return response;
                    response.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Proxy failed for {Path}. Trying next.", path);
                    lastException = ex;
                }
            }

            if (lastException != null) throw lastException;
            throw new HttpRequestException($"All proxy backends failed for {path}");
        }
    }
}
