using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PocketMC.Core.Models;

namespace PocketMC.Infrastructure.Services;

public class DownloaderService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DownloaderService> _logger;

    public DownloaderService(HttpClient httpClient, ILogger<DownloaderService>? logger = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _logger = logger ?? NullLogger<DownloaderService>.Instance;
    }

    public Task DownloadFileAsync(
        string url,
        string destinationPath,
        string? expectedHash = null,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return DownloadFileAsync(url, destinationPath, expectedHash, null, progress, cancellationToken);
    }

    public async Task DownloadFileAsync(
        string url,
        string destinationPath,
        string? expectedHash,
        string? expectedHashType,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string partialPath = destinationPath + ".partial";
        string identityPath = partialPath + ".identity";
        if (!File.Exists(identityPath) || File.ReadAllText(identityPath) != url)
        {
            TryDeleteFile(partialPath);
            File.WriteAllText(identityPath, url);
        }

        string? directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        const int maxAttempts = 4;
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                long existingBytes = GetExistingPartialLength(partialPath);
                using HttpRequestMessage request = new(HttpMethod.Get, url);
                if (existingBytes > 0 && string.IsNullOrEmpty(expectedHash))
                {
                    TryDeleteFile(partialPath);
                    existingBytes = 0;
                }

                if (existingBytes > 0)
                {
                    request.Headers.Range = new RangeHeaderValue(existingBytes, null);
                }

                using HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (existingBytes > 0 && response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
                {
                    _logger.LogInformation("Server rejected resume for {Url}; restarting download.", url);
                    TryDeleteFile(partialPath);
                    existingBytes = 0;
                    using HttpRequestMessage restartRequest = new(HttpMethod.Get, url);
                    using HttpResponseMessage restartResponse = await _httpClient.SendAsync(restartRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    restartResponse.EnsureSuccessStatusCode();
                    await DownloadToPartialAsync(restartResponse, partialPath, existingBytes, progress, cancellationToken);
                }
                else
                {
                    response.EnsureSuccessStatusCode();

                    bool isResuming = existingBytes > 0 && response.StatusCode == HttpStatusCode.PartialContent;
                    if (existingBytes > 0 && !isResuming)
                    {
                        _logger.LogInformation("Resume is not supported for {Url}; restarting download.", url);
                        TryDeleteFile(partialPath);
                        existingBytes = 0;
                    }

                    await DownloadToPartialAsync(response, partialPath, existingBytes, progress, cancellationToken);
                }

                if (new FileInfo(partialPath).Length == 0)
                {
                    throw new InvalidDataException("Downloaded artifact is empty.");
                }

                if (!string.IsNullOrEmpty(expectedHash))
                {
                    string hashType = NormalizeHashType(expectedHash, expectedHashType);
                    _logger.LogInformation("Verifying {HashType} hash for {DestinationPath}...", hashType, destinationPath);
                    bool isValid = await VerifyHashAsync(partialPath, expectedHash, hashType, cancellationToken);
                    if (!isValid)
                    {
                        TryDeleteFile(partialPath);
                        throw new CryptographicException($"{hashType} verification failed for {url}. Expected {expectedHash}.");
                    }
                }

                PromoteCompletedDownload(partialPath, destinationPath);
                TryDeleteFile(identityPath);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Download attempt {Attempt}/{MaxAttempts} failed for {Url}. Retrying...", attempt, maxAttempts, url);
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
            }
            catch (Exception ex)
            {
                lastException = ex;
                break;
            }
        }

        throw new InvalidOperationException($"Failed to download '{url}'.", lastException);
    }

    private static long GetExistingPartialLength(string path)
    {
        return File.Exists(path) ? new FileInfo(path).Length : 0;
    }

    private static async Task DownloadToPartialAsync(
        HttpResponseMessage response,
        string partialPath,
        long existingBytes,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        long totalBytes = response.Content.Headers.ContentLength ?? -1L;
        if (totalBytes > 0 && existingBytes > 0)
        {
            totalBytes += existingBytes;
        }

        await using Stream contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using FileStream fileStream = new(
            partialPath,
            existingBytes > 0 ? FileMode.Append : FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        byte[] buffer = new byte[81920];
        long totalRead = existingBytes;
        int read;

        while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            totalRead += read;

            progress?.Report(new DownloadProgress
            {
                BytesRead = totalRead,
                TotalBytes = totalBytes
            });
        }
    }

    private static void PromoteCompletedDownload(string partialPath, string destinationPath)
    {
        if (File.Exists(destinationPath))
        {
            string backupPath = destinationPath + ".bak";
            try
            {
                File.Replace(partialPath, destinationPath, backupPath, ignoreMetadataErrors: true);
                TryDeleteFile(backupPath);
            }
            catch
            {
                File.Delete(destinationPath);
                File.Move(partialPath, destinationPath);
            }
        }
        else
        {
            File.Move(partialPath, destinationPath);
        }
    }

    private static string NormalizeHashType(string expectedHash, string? expectedHashType)
    {
        if (!string.IsNullOrWhiteSpace(expectedHashType))
        {
            return expectedHashType.Replace("-", "").ToUpperInvariant();
        }

        return expectedHash.Length switch
        {
            40 => "SHA1",
            64 => "SHA256",
            128 => "SHA512",
            _ => throw new NotSupportedException($"Unsupported hash length '{expectedHash.Length}'.")
        };
    }

    private static async Task<bool> VerifyHashAsync(string filePath, string expectedHash, string hashType, CancellationToken cancellationToken)
    {
        using HashAlgorithm hasher = hashType switch
        {
            "SHA1" => SHA1.Create(),
            "SHA256" => SHA256.Create(),
            "SHA512" => SHA512.Create(),
            _ => throw new NotSupportedException($"Unsupported hash type '{hashType}'.")
        };

        await using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        byte[] hashBytes = await hasher.ComputeHashAsync(stream, cancellationToken);
        string actualHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

        return string.Equals(actualHash, expectedHash.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}
