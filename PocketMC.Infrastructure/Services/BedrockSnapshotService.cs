using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using PocketMC.Core.Models;
using PocketMC.Core.Services;

namespace PocketMC.Infrastructure.Services;

public static class BedrockSnapshotService
{
    public static async Task<string> StageAsync(
        IProcessRunner processRunner,
        IConsoleLogService logService,
        ServerInstance instance,
        string worldDirectory,
        string stagingDirectory,
        CancellationToken cancellationToken = default,
        TimeSpan? responseTimeout = null)
    {
        if (!processRunner.TryGetRunningInfo(instance.Slug, out _, out string state) ||
            !string.Equals(state, "Running", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("BDS must be running before taking a live snapshot.");
        }

        var lines = Channel.CreateBounded<string>(256);
        bool overflow = false;

        void Receive(string slug, string line)
        {
            if (!string.Equals(slug, instance.Slug, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (line.Length > 1_048_576 || !lines.Writer.TryWrite(line))
            {
                overflow = true;
            }
        }

        logService.LogReceived += Receive;
        bool holdAttempted = false;

        try
        {
            holdAttempted = true;
            await processRunner.SendCommandAsync(instance, "save hold")
                .WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(responseTimeout ?? TimeSpan.FromSeconds(30));

            var entries = new List<(string Path, long Length)>();
            bool ready = false;

            while (entries.Count == 0)
            {
                deadline.Token.ThrowIfCancellationRequested();
                if (!processRunner.TryGetRunningInfo(instance.Slug, out _, out string currentState) ||
                    !string.Equals(currentState, "Running", StringComparison.OrdinalIgnoreCase) ||
                    overflow)
                {
                    throw new InvalidOperationException("BDS snapshot output was interrupted or exceeded its safe limit.");
                }

                await processRunner.SendCommandAsync(instance, "save query")
                    .WaitAsync(TimeSpan.FromSeconds(5), deadline.Token);

                using var window = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
                window.CancelAfter(TimeSpan.FromSeconds(1));
                try
                {
                    while (entries.Count == 0)
                    {
                        string line = NormalizeConsoleLine(await lines.Reader.ReadAsync(window.Token));
                        if (line.Equals("Data saved. Files are now ready to be copied.", StringComparison.OrdinalIgnoreCase))
                        {
                            ready = true;
                            continue;
                        }

                        if (!ready || !line.Contains(':', StringComparison.Ordinal))
                        {
                            continue;
                        }

                        foreach (string entry in line.Split(", ", StringSplitOptions.RemoveEmptyEntries))
                        {
                            int delimiter = entry.LastIndexOf(':');
                            if (delimiter <= 0 ||
                                !long.TryParse(entry[(delimiter + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out long length))
                            {
                                throw new InvalidDataException("BDS returned an invalid snapshot file list.");
                            }

                            entries.Add((entry[..delimiter], length));
                        }
                    }
                }
                catch (OperationCanceledException) when (!deadline.IsCancellationRequested)
                {
                }
            }

            string worldRelative = Path.GetRelativePath(instance.Path, worldDirectory)
                .Replace('\\', '/')
                .TrimEnd('/');
            string worldLeaf = Path.GetFileName(worldDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            string stagedWorldDir = PathSafety.ValidateContainedPath(stagingDirectory, worldRelative)
                ?? throw new InvalidDataException("Unsafe staged world path.");

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            byte[] buffer = new byte[81920];

            foreach ((string rawEntryPath, long expectedLength) in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string relative = rawEntryPath.Replace('\\', '/');
                string? worldRelativeFile = ExtractWorldRelativePath(relative, worldRelative, worldLeaf);
                if (worldRelativeFile == null || !seen.Add(worldRelativeFile))
                {
                    throw new InvalidDataException("The BDS snapshot contains a duplicate file or a file outside the configured world.");
                }

                string source = PathSafety.ValidateContainedPath(worldDirectory, worldRelativeFile)
                    ?? throw new InvalidDataException("Unsafe BDS snapshot source.");
                string destination = PathSafety.ValidateContainedPath(stagedWorldDir, worldRelativeFile)
                    ?? throw new InvalidDataException("Unsafe BDS snapshot destination.");

                if (PathSafety.HasReparsePoint(source))
                {
                    throw new InvalidDataException("Symbolic links are not permitted in BDS snapshot sources.");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                if (PathSafety.HasReparsePoint(destination))
                {
                    throw new InvalidDataException("Symbolic links are not permitted in BDS snapshot destinations.");
                }

                await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (input.Length < expectedLength)
                {
                    throw new EndOfStreamException("A BDS snapshot file is shorter than the confirmed length.");
                }

                await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                long remaining = expectedLength;
                while (remaining > 0)
                {
                    int readCount = await input.ReadAsync(
                        buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)),
                        cancellationToken);
                    if (readCount == 0)
                    {
                        throw new EndOfStreamException("BDS snapshot copying ended before the confirmed length.");
                    }

                    await output.WriteAsync(buffer.AsMemory(0, readCount), cancellationToken);
                    remaining -= readCount;
                }
            }

            if (!File.Exists(Path.Combine(stagedWorldDir, "level.dat")))
            {
                throw new InvalidDataException("The confirmed BDS snapshot does not contain level.dat; a stopped backup is required.");
            }

            return stagedWorldDir;
        }
        finally
        {
            try
            {
                if (holdAttempted)
                {
                    while (lines.Reader.TryRead(out _))
                    {
                    }

                    using var resumeTimeout = new CancellationTokenSource(responseTimeout ?? TimeSpan.FromSeconds(10));
                    await processRunner.SendCommandAsync(instance, "save resume").WaitAsync(resumeTimeout.Token);
                    while (true)
                    {
                        string line = NormalizeConsoleLine(await lines.Reader.ReadAsync(resumeTimeout.Token));
                        if (line.Equals("Changes to the level are resumed.", StringComparison.OrdinalIgnoreCase))
                        {
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "BDS did not confirm that world saving resumed. Run save resume on the server immediately. The backup was not published.",
                    ex);
            }
            finally
            {
                logService.LogReceived -= Receive;
                lines.Writer.TryComplete();
            }
        }
    }

    private static string NormalizeConsoleLine(string line)
    {
        if (line.StartsWith('[') && line.IndexOf("] ", StringComparison.Ordinal) is int prefix && prefix >= 0)
        {
            return line[(prefix + 2)..];
        }

        return line;
    }

    private static string? ExtractWorldRelativePath(string normalizedEntry, string worldRelative, string worldLeaf)
    {
        if (PathSafety.ContainsTraversal(normalizedEntry))
        {
            return null;
        }

        string relPrefix = worldRelative + "/";
        if (normalizedEntry.StartsWith(relPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return normalizedEntry[relPrefix.Length..];
        }

        if (!string.IsNullOrEmpty(worldLeaf) &&
            !string.Equals(worldRelative, worldLeaf, StringComparison.OrdinalIgnoreCase))
        {
            string leafPrefix = worldLeaf + "/";
            if (normalizedEntry.StartsWith(leafPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return normalizedEntry[leafPrefix.Length..];
            }
        }

        return null;
    }
}
