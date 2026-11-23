using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using PocketMC.Core.Models;
using PocketMC.Core.Services;

namespace PocketMC.Infrastructure.Services
{
    public class ServerRebootSchedulerService : IDisposable
    {
        private readonly System.Timers.Timer _timer;
        private readonly IInstanceService _instanceService;
        private readonly IProcessRunner _processRunner;
        private readonly IRebootScheduleService? _schedules;
        private readonly ConcurrentDictionary<string, InFlightReboot> _inFlightReboots = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, DateTime> _failedAttempts = new(StringComparer.OrdinalIgnoreCase);
        private int _isProcessing;
        private bool _isDisposed;

        public sealed class InFlightReboot
        {
            public required CancellationTokenSource Cts { get; init; }
            public Task? Task { get; set; }
        }

        public ServerRebootSchedulerService(
            IInstanceService instanceService,
            IProcessRunner processRunner,
            IRebootScheduleService? schedules = null)
        {
            _instanceService = instanceService ?? throw new ArgumentNullException(nameof(instanceService));
            _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
            _schedules = schedules;

            if (_schedules != null)
            {
                _schedules.ScheduleChanged += AbortScheduledReboot;
            }

            _processRunner.StateChanged += HandleInstanceStateChanged;

            _timer = new System.Timers.Timer(30_000);
            _timer.Elapsed += OnTimerTick;
            _timer.AutoReset = true;
        }

        public void Start()
        {
            if (_isDisposed) return;
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
            AbortAllPendingReboots();
        }

        public bool IsRebootPending(string slug) => _inFlightReboots.ContainsKey(slug);

        public void AbortScheduledReboot(string slug)
        {
            if (_inFlightReboots.TryRemove(slug, out var inFlight))
            {
                try
                {
                    inFlight.Cts.Cancel();
                }
                catch (ObjectDisposedException) { }
            }
        }

        private Task[] AbortAllPendingReboots()
        {
            var tasks = new List<Task>();
            foreach (var kvp in _inFlightReboots.ToArray())
            {
                if (_inFlightReboots.TryRemove(kvp.Key, out var inFlight))
                {
                    try
                    {
                        inFlight.Cts.Cancel();
                    }
                    catch (ObjectDisposedException) { }

                    if (inFlight.Task != null)
                    {
                        tasks.Add(inFlight.Task);
                    }
                }
            }
            return tasks.ToArray();
        }

        private void HandleInstanceStateChanged(string slug, string state)
        {
            if (string.Equals(state, "Stopping", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(state, "Stopped", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(state, "Crashed", StringComparison.OrdinalIgnoreCase))
            {
                AbortScheduledReboot(slug);
            }
        }

        private async void OnTimerTick(object? sender, ElapsedEventArgs e)
        {
            await EvaluateDueInstancesAsync(DateTime.UtcNow);
        }

        public async Task EvaluateDueInstancesAsync(DateTime nowUtc)
        {
            if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) != 0)
            {
                return;
            }

            try
            {
                var instances = await _instanceService.ListInstancesAsync();
                foreach (var inst in instances)
                {
                    if (_inFlightReboots.ContainsKey(inst.Slug))
                    {
                        continue;
                    }

                    if (IsDue(inst, nowUtc))
                    {
                        var cts = new CancellationTokenSource();
                        var inFlight = new InFlightReboot { Cts = cts };
                        if (_inFlightReboots.TryAdd(inst.Slug, inFlight))
                        {
                            inFlight.Task = Task.Run(() => ExecuteScheduledRebootAsync(inst, cts));
                        }
                        else
                        {
                            cts.Dispose();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PocketMC] Error during scheduled reboot evaluation: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _isProcessing, 0);
            }
        }

        public bool IsDue(ServerInstance instance, DateTime nowUtc)
        {
            if (instance?.RebootSchedule == null || !instance.RebootSchedule.Enabled)
            {
                return false;
            }

            if (_failedAttempts.TryGetValue(instance.Slug, out var failedAt) && nowUtc - failedAt < TimeSpan.FromMinutes(5))
            {
                return false;
            }

            if (!_processRunner.TryGetRunningInfo(instance.Slug, out _, out var state) ||
                !string.Equals(state, "Running", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var sessionStart = _processRunner.GetSessionStartTime(instance.Slug);
            var minUptime = TimeSpan.FromMinutes(1);
            if (sessionStart.HasValue && (nowUtc - sessionStart.Value) < minUptime)
            {
                return false;
            }

            var schedule = instance.RebootSchedule;
            if (string.Equals(schedule.Mode, "Interval", StringComparison.OrdinalIgnoreCase))
            {
                int intervalHours = schedule.IntervalHours > 0 ? schedule.IntervalHours : 24;
                DateTime referenceTime = instance.LastScheduledRebootTime ?? sessionStart ?? nowUtc;
                DateTime dueTime = referenceTime.AddHours(intervalHours);
                return nowUtc >= dueTime;
            }
            else
            {
                if (!TryParseTimeOfDay(schedule.Time, out TimeSpan targetTimeOfDay))
                {
                    targetTimeOfDay = new TimeSpan(4, 0, 0);
                }

                DateTime nowLocal = nowUtc.ToLocalTime();
                DateTime todayTargetLocal = nowLocal.Date.Add(targetTimeOfDay);

                if (nowLocal < todayTargetLocal)
                {
                    return false;
                }

                if (instance.LastScheduledRebootTime.HasValue)
                {
                    DateTime lastLocal = instance.LastScheduledRebootTime.Value.ToLocalTime();
                    if (lastLocal.Date == nowLocal.Date && lastLocal >= todayTargetLocal)
                    {
                        return false;
                    }
                }

                if (sessionStart.HasValue)
                {
                    DateTime startLocal = sessionStart.Value.ToLocalTime();
                    if (startLocal.Date == nowLocal.Date && startLocal >= todayTargetLocal)
                    {
                        return false;
                    }
                }

                if (nowLocal > todayTargetLocal.AddMinutes(30))
                {
                    return false;
                }

                return true;
            }
        }

        public static DateTime? GetNextRebootTimeUtc(ServerInstance instance, DateTime nowUtc)
        {
            if (instance?.RebootSchedule == null || !instance.RebootSchedule.Enabled)
            {
                return null;
            }

            var schedule = instance.RebootSchedule;
            if (string.Equals(schedule.Mode, "Interval", StringComparison.OrdinalIgnoreCase))
            {
                int intervalHours = schedule.IntervalHours > 0 ? schedule.IntervalHours : 24;
                DateTime referenceTime = instance.LastScheduledRebootTime ?? nowUtc;
                return referenceTime.AddHours(intervalHours);
            }
            else
            {
                if (!TryParseTimeOfDay(schedule.Time, out TimeSpan targetTimeOfDay))
                {
                    targetTimeOfDay = new TimeSpan(4, 0, 0);
                }

                DateTime nowLocal = nowUtc.ToLocalTime();
                DateTime todayTargetLocal = nowLocal.Date.Add(targetTimeOfDay);

                DateTime nextLocal = nowLocal < todayTargetLocal
                    ? todayTargetLocal
                    : todayTargetLocal.AddDays(1);

                return nextLocal.ToUniversalTime();
            }
        }

        public static bool TryParseTimeOfDay(string? input, out TimeSpan timeOfDay)
        {
            timeOfDay = TimeSpan.Zero;
            if (string.IsNullOrWhiteSpace(input)) return false;
            input = input.Trim();

            string[] formats = { "h\\:mm", "hh\\:mm", "H\\:mm", "HH\\:mm", "h\\:mm\\:ss", "hh\\:mm\\:ss", "H\\:mm\\:ss", "HH\\:mm\\:ss" };
            if (TimeSpan.TryParseExact(input, formats, CultureInfo.InvariantCulture, out timeOfDay))
            {
                return true;
            }

            if (DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.NoCurrentDateDefault, out DateTime dt))
            {
                timeOfDay = dt.TimeOfDay;
                return true;
            }

            return false;
        }

        public async Task ExecuteScheduledRebootAsync(ServerInstance instance, CancellationTokenSource cts)
        {
            var originalSchedule = instance.RebootSchedule;
            bool SettingsChanged()
            {
                var instances = _instanceService.ListInstancesAsync().GetAwaiter().GetResult();
                var current = instances.FirstOrDefault(i => i.Slug.Equals(instance.Slug, StringComparison.OrdinalIgnoreCase));
                return current?.RebootSchedule == null ||
                       !current.RebootSchedule.Enabled ||
                       current.RebootSchedule != originalSchedule;
            }

            try
            {
                if (cts.Token.IsCancellationRequested || SettingsChanged()) return;

                int warningSeconds = originalSchedule?.WarningSeconds > 0 ? originalSchedule.WarningSeconds : 60;
                bool warningEnabled = originalSchedule?.WarningEnabled ?? false;

                if (warningEnabled && warningSeconds > 0)
                {
                    await BroadcastRebootWarningAsync(instance, $"Scheduled server reboot in {warningSeconds} seconds!");

                    int remaining = warningSeconds;
                    while (remaining > 0)
                    {
                        if (cts.Token.IsCancellationRequested || SettingsChanged())
                        {
                            return;
                        }

                        if (remaining is 60 or 30 or 15 or 10 or 5 or 4 or 3 or 2 or 1 && remaining < warningSeconds)
                        {
                            await BroadcastRebootWarningAsync(instance, $"Server rebooting in {remaining} seconds!");
                        }

                        await Task.Delay(1000, cts.Token);
                        remaining--;
                    }

                    if (cts.Token.IsCancellationRequested || SettingsChanged()) return;

                    await BroadcastRebootWarningAsync(instance, "Server is rebooting now...");
                    await Task.Delay(500, cts.Token);
                }

                if (cts.Token.IsCancellationRequested || SettingsChanged()) return;

                await _processRunner.StopAsync(instance);
                await Task.Delay(1000, cts.Token);
                await _processRunner.StartAsync(instance);

                instance.LastScheduledRebootTime = DateTime.UtcNow;
                _failedAttempts.TryRemove(instance.Slug, out _);
                await _instanceService.SaveInstanceAsync(instance);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _failedAttempts[instance.Slug] = DateTime.UtcNow;
                Console.WriteLine($"[PocketMC] Scheduled reboot failed for instance '{instance.Slug}': {ex.Message}");
            }
            finally
            {
                if (_inFlightReboots.TryGetValue(instance.Slug, out var current) && ReferenceEquals(current.Cts, cts))
                {
                    _inFlightReboots.TryRemove(new KeyValuePair<string, InFlightReboot>(instance.Slug, current));
                }
                cts.Dispose();
            }
        }

        private async Task BroadcastRebootWarningAsync(ServerInstance instance, string message)
        {
            try
            {
                await _processRunner.SendCommandAsync(instance, $"say [PocketMC] {message}");
            }
            catch { }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _processRunner.StateChanged -= HandleInstanceStateChanged;
            if (_schedules != null)
            {
                _schedules.ScheduleChanged -= AbortScheduledReboot;
            }

            _timer.Stop();
            _timer.Dispose();
            AbortAllPendingReboots();
        }
    }
}
