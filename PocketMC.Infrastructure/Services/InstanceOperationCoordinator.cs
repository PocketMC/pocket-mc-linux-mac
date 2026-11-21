using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PocketMC.Infrastructure.Services
{
    /// <summary>
    /// Coordinates operations (updates, backups, startup) on instance directories.
    /// </summary>
    public sealed class InstanceOperationCoordinator
    {
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, byte> Running = new(StringComparer.OrdinalIgnoreCase);
        private static readonly SemaphoreSlim Admissions = new(1, 1);

        public async Task<IDisposable> AcquireAsync(string directory, bool requiresStopped = false, CancellationToken cancellationToken = default)
        {
            string key = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            await Admissions.WaitAsync(cancellationToken);
            SemaphoreSlim gate;
            try
            {
                gate = Gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            }
            finally
            {
                Admissions.Release();
            }

            await gate.WaitAsync(cancellationToken);

            if (requiresStopped && Running.ContainsKey(key))
            {
                gate.Release();
                throw new InvalidOperationException("The server must be stopped before performing file or version changes.");
            }

            return new Release(gate);
        }

        public void SetRunning(string directory, bool isRunning)
        {
            string key = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (isRunning)
            {
                Running[key] = 1;
            }
            else
            {
                Running.TryRemove(key, out _);
            }
        }

        private sealed class Release : IDisposable
        {
            private readonly SemaphoreSlim _gate;
            private int _released;

            public Release(SemaphoreSlim gate)
            {
                _gate = gate;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _released, 1) == 0)
                {
                    _gate.Release();
                }
            }
        }
    }
}
