using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PocketMC.Core.Models;
using PocketMC.Core.Services;

namespace PocketMC.Infrastructure.Services
{
    public class RebootScheduleService : IRebootScheduleService
    {
        private readonly IInstanceService _instanceService;
        public event Action<string>? ScheduleChanged;

        public RebootScheduleService(IInstanceService instanceService)
        {
            _instanceService = instanceService ?? throw new ArgumentNullException(nameof(instanceService));
        }

        public RebootScheduleSettings? GetSchedule(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug)) return null;
            var instances = _instanceService.ListInstancesAsync().GetAwaiter().GetResult();
            var instance = instances.FirstOrDefault(i => i.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
            return instance?.RebootSchedule;
        }

        public async Task SaveScheduleAsync(string slug, RebootScheduleSettings schedule, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(schedule);
            if (string.IsNullOrWhiteSpace(slug)) throw new ArgumentException("Slug cannot be empty.", nameof(slug));

            string mode = schedule.Mode?.Trim() ?? string.Empty;
            if (!mode.Equals("Daily", StringComparison.OrdinalIgnoreCase) && !mode.Equals("Interval", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Choose Daily or Interval schedule mode.");
            }

            if (!TimeSpan.TryParseExact(schedule.Time, @"hh\:mm", CultureInfo.InvariantCulture, out var time) || time.TotalHours >= 24)
            {
                throw new ArgumentException("Use a host-local time between 00:00 and 23:59.");
            }

            if (schedule.IntervalHours is < 1 or > 8760)
            {
                throw new ArgumentException("Choose a reboot interval between 1 and 8760 hours.");
            }

            if (schedule.WarningSeconds is < 0 or > 3600 || (schedule.WarningEnabled && schedule.WarningSeconds == 0))
            {
                throw new ArgumentException("Choose a warning duration between 1 and 3600 seconds, or disable warnings.");
            }

            var instances = await _instanceService.ListInstancesAsync();
            var instance = instances.FirstOrDefault(i => i.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
            if (instance == null)
            {
                throw new System.Collections.Generic.KeyNotFoundException($"Instance with slug '{slug}' not found.");
            }

            var normalizedSchedule = schedule with
            {
                Mode = mode.Equals("Daily", StringComparison.OrdinalIgnoreCase) ? "Daily" : "Interval",
                Time = time.ToString(@"hh\:mm", CultureInfo.InvariantCulture)
            };

            instance.RebootSchedule = normalizedSchedule;
            await _instanceService.SaveInstanceAsync(instance);
            ScheduleChanged?.Invoke(slug);
        }
    }
}
