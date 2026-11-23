using System;
using System.Threading;
using System.Threading.Tasks;
using PocketMC.Core.Models;

namespace PocketMC.Core.Services
{
    public interface IRebootScheduleService
    {
        event Action<string>? ScheduleChanged;
        RebootScheduleSettings? GetSchedule(string slug);
        Task SaveScheduleAsync(string slug, RebootScheduleSettings schedule, CancellationToken cancellationToken = default);
    }
}
