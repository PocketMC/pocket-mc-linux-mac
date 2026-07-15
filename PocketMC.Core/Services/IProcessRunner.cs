using System;
using System.Threading.Tasks;
using PocketMC.Core.Models;

namespace PocketMC.Core.Services
{
    public interface IProcessRunner
    {
        Task StartAsync(ServerInstance instance, bool isAutoRestart = false);
        Task StopAsync(ServerInstance instance);
        Task SendCommandAsync(ServerInstance instance, string command);
        bool TryGetRunningInfo(string slug, out int pgid, out string state);
        event Action<string, string> StateChanged; // Instance slug, state string
    }
}
