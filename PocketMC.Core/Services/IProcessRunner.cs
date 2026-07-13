using System;
using System.Threading.Tasks;
using PocketMC.Core.Models;

namespace PocketMC.Core.Services
{
    public interface IProcessRunner
    {
        Task StartAsync(ServerInstance instance);
        Task StopAsync(ServerInstance instance);
        Task SendCommandAsync(ServerInstance instance, string command);
        event Action<string, string> StateChanged; // Instance slug, state string
    }
}
