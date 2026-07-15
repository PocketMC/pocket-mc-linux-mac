using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PocketMC.App.ViewModels;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using Xunit;

namespace PocketMC.Tests
{
    public class FakeConsoleLogService : IConsoleLogService
    {
        public List<string> Logs { get; } = new();
        public event Action<string, string>? LogReceived;

        public void WriteLog(string slug, string line)
        {
            Logs.Add(line);
            LogReceived?.Invoke(slug, line);
        }

        public IReadOnlyList<string> GetLogs(string slug) => Logs;
        public IReadOnlyList<string> SearchLogs(string slug, string query) => Logs.Where(l => l.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        public void TriggerLogReceived(string slug, string line)
        {
            LogReceived?.Invoke(slug, line);
        }
    }

    public class ServerConsoleViewModelTests
    {
        [Fact]
        public void ServerConsoleViewModel_ReceivesLogsForSelectedInstance()
        {
            var instService = new FakeInstanceService();
            var inst1 = new ServerInstance { Name = "Server 1", Slug = "server-1" };
            var inst2 = new ServerInstance { Name = "Server 2", Slug = "server-2" };
            instService.Instances.Add(inst1);
            instService.Instances.Add(inst2);

            var logService = new FakeConsoleLogService();
            var processRunner = new FakeProcessRunner();

            using var vm = new ServerConsoleViewModel(instService, logService, processRunner);
            vm.SelectedInstance = inst1;

            // Trigger log for inst1 (selected)
            logService.TriggerLogReceived("server-1", "Inst 1 Log Message");
            // Trigger log for inst2 (not selected)
            logService.TriggerLogReceived("server-2", "Inst 2 Log Message");

            Assert.Contains(vm.LogLines, l => l.Text == "Inst 1 Log Message");
            Assert.DoesNotContain(vm.LogLines, l => l.Text == "Inst 2 Log Message");
        }

        [Fact]
        public void ServerConsoleViewModel_AppliesSearchFilter()
        {
            var instService = new FakeInstanceService();
            var inst = new ServerInstance { Name = "Server 1", Slug = "server-1" };
            instService.Instances.Add(inst);

            var logService = new FakeConsoleLogService();
            logService.Logs.Add("Error in module A");
            logService.Logs.Add("Normal output B");
            logService.Logs.Add("Error in module C");

            var processRunner = new FakeProcessRunner();

            using var vm = new ServerConsoleViewModel(instService, logService, processRunner);
            vm.SelectedInstance = inst;

            Assert.Equal(3, vm.LogLines.Count);

            // Filter for "Error"
            vm.SearchQuery = "Error";
            Assert.Equal(2, vm.LogLines.Count);
            Assert.All(vm.LogLines, l => Assert.Contains("Error", l.Text));
        }

        [Fact]
        public async Task ServerConsoleViewModel_CommandHistoryCycling()
        {
            var instService = new FakeInstanceService();
            var inst = new ServerInstance { Name = "Server 1", Slug = "server-1" };
            instService.Instances.Add(inst);

            var logService = new FakeConsoleLogService();
            var processRunner = new FakeProcessRunner();

            using var vm = new ServerConsoleViewModel(instService, logService, processRunner);
            vm.SelectedInstance = inst;

            // Send 3 commands
            vm.CommandInput = "command 1";
            await vm.SendCommandCommand.ExecuteAsync(null);

            vm.CommandInput = "command 2";
            await vm.SendCommandCommand.ExecuteAsync(null);

            vm.CommandInput = "command 3";
            await vm.SendCommandCommand.ExecuteAsync(null);

            // Cycle history up
            Assert.Equal("command 3", vm.CycleHistory(true));
            Assert.Equal("command 2", vm.CycleHistory(true));
            Assert.Equal("command 1", vm.CycleHistory(true));

            // Cycle history down
            Assert.Equal("command 2", vm.CycleHistory(false));
            Assert.Equal("command 3", vm.CycleHistory(false));
            Assert.Equal("", vm.CycleHistory(false));
        }
    }
}
