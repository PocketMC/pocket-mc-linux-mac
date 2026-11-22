using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PocketMC.Core.Models.Networking;
using PocketMC.Core.Services;

namespace PocketMC.Platform.Networking;

public sealed class PosixPortOwnerResolver : IPosixPortOwnerResolver
{
    private static readonly Regex SsUserRegex = new(@"users:\(\(""([^""]+)"",pid=(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(500));
    private static readonly Regex LsofLineRegex = new(@"^(\S+)\s+(\d+)\s+", RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(500));

    public async Task<(int? ProcessId, string? ProcessName)> ResolveOwnerAsync(int port, PortProtocol protocol, CancellationToken cancellationToken = default)
    {
        if (port < 1 || port > 65535)
        {
            return (null, null);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var ssResult = await TryResolveLinuxSsAsync(port, protocol, cancellationToken).ConfigureAwait(false);
            if (ssResult.ProcessId.HasValue)
            {
                return ssResult;
            }

            return await TryResolveLsofAsync(port, protocol, cancellationToken).ConfigureAwait(false);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return await TryResolveLsofAsync(port, protocol, cancellationToken).ConfigureAwait(false);
        }

        return (null, null);
    }

    public static bool TryParseSsOutput(string output, int port, out int processId, out string processName)
    {
        processId = 0;
        processName = string.Empty;

        if (string.IsNullOrWhiteSpace(output))
        {
            return false;
        }

        string portSuffix = $":{port}";
        using var reader = new StringReader(output);
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            if (!line.Contains(portSuffix, StringComparison.Ordinal))
            {
                continue;
            }

            var match = SsUserRegex.Match(line);
            if (match.Success &&
                int.TryParse(match.Groups[2].Value, out int pid))
            {
                processId = pid;
                processName = match.Groups[1].Value;
                return true;
            }
        }

        return false;
    }

    public static bool TryParseLsofOutput(string output, int port, out int processId, out string processName)
    {
        processId = 0;
        processName = string.Empty;

        if (string.IsNullOrWhiteSpace(output))
        {
            return false;
        }

        string portPattern = $":{port}";
        using var reader = new StringReader(output);
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            if (line.StartsWith("COMMAND", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!line.Contains(portPattern, StringComparison.Ordinal))
            {
                continue;
            }

            var match = LsofLineRegex.Match(line.TrimStart());
            if (match.Success &&
                int.TryParse(match.Groups[2].Value, out int pid))
            {
                processId = pid;
                processName = match.Groups[1].Value;
                return true;
            }
        }

        return false;
    }

    private static async Task<(int? ProcessId, string? ProcessName)> TryResolveLinuxSsAsync(int port, PortProtocol protocol, CancellationToken cancellationToken)
    {
        string flag = protocol == PortProtocol.Udp ? "-ulpn" : "-tlpn";
        string? output = await RunCommandAsync("ss", flag, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(output) &&
            TryParseSsOutput(output, port, out int pid, out string name))
        {
            return (pid, name);
        }

        return (null, null);
    }

    private static async Task<(int? ProcessId, string? ProcessName)> TryResolveLsofAsync(int port, PortProtocol protocol, CancellationToken cancellationToken)
    {
        string proto = protocol == PortProtocol.Udp ? $"iUDP:{port}" : $"iTCP:{port}";
        string args = protocol == PortProtocol.Udp
            ? $"-{proto} -P -n"
            : $"-{proto} -sTCP:LISTEN -P -n";

        string? output = await RunCommandAsync("lsof", args, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(output) &&
            TryParseLsofOutput(output, port, out int pid, out string name))
        {
            return (pid, name);
        }

        return (null, null);
    }

    private static async Task<string?> RunCommandAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            var readOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var completedTask = await Task.WhenAny(readOutputTask, Task.Delay(1500, cancellationToken)).ConfigureAwait(false);

            if (completedTask == readOutputTask && process.WaitForExit(1000))
            {
                return await readOutputTask.ConfigureAwait(false);
            }

            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch { }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
