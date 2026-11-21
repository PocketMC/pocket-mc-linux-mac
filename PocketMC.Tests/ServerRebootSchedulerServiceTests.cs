using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;
using Xunit;

namespace PocketMC.Tests
{
    public class ServerRebootSchedulerServiceTests
    {
        private class TestRunner : IProcessRunner
        {
            public event Action<string, string>? StateChanged;
            public bool IsRunning { get; set; } = true;
            public string RunningState { get; set; } = "Running";
            public DateTime? SessionStart { get; set; } = DateTime.UtcNow.AddHours(-2);
            public List<string> CommandsSent { get; } = new();
            public int StopCount { get; set; }
            public int StartCount { get; set; }

            public Task StartAsync(ServerInstance instance, bool isAutoRestart = false)
            {
                StartCount++;
                IsRunning = true;
                RunningState = "Running";
                SessionStart = DateTime.UtcNow;
                StateChanged?.Invoke(instance.Slug, "Running");
                return Task.CompletedTask;
            }

            public Task StopAsync(ServerInstance instance)
            {
                StopCount++;
                IsRunning = false;
                RunningState = "Stopped";
                StateChanged?.Invoke(instance.Slug, "Stopped");
                return Task.CompletedTask;
            }

            public Task StopAllAsync() => Task.CompletedTask;
            public IReadOnlyList<string> GetRunningSlugs() => IsRunning ? new[] { "test-server" } : Array.Empty<string>();

            public Task SendCommandAsync(ServerInstance instance, string command)
            {
                CommandsSent.Add(command);
                return Task.CompletedTask;
            }

            public bool TryGetRunningInfo(string slug, out int pgid, out string state)
            {
                pgid = IsRunning ? 1234 : 0;
                state = RunningState;
                return IsRunning;
            }

            public DateTime? GetSessionStartTime(string slug) => SessionStart;
        }

        [Theory]
        [InlineData("04:00", 4, 0)]
        [InlineData("4:00", 4, 0)]
        [InlineData("16:30", 16, 30)]
        [InlineData("04:30:00", 4, 30)]
        [InlineData("4:15 PM", 16, 15)]
        [InlineData("4:15 AM", 4, 15)]
        public void TryParseTimeOfDay_ValidFormats_ParsesCorrectly(string input, int expectedHour, int expectedMinute)
        {
            bool success = ServerRebootSchedulerService.TryParseTimeOfDay(input, out TimeSpan timeOfDay);
            Assert.True(success);
            Assert.Equal(expectedHour, timeOfDay.Hours);
            Assert.Equal(expectedMinute, timeOfDay.Minutes);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("invalid")]
        [InlineData("25:00")]
        [InlineData("4:65")]
        public void TryParseTimeOfDay_InvalidFormats_ReturnsFalse(string? input)
        {
            bool success = ServerRebootSchedulerService.TryParseTimeOfDay(input, out _);
            Assert.False(success);
        }

        [Fact]
        public void IsDue_WhenDisabled_ReturnsFalse()
        {
            var runner = new TestRunner();
            var instanceService = new FakeInstanceService();
            using var scheduler = new ServerRebootSchedulerService(instanceService, runner);

            var inst = new ServerInstance
            {
                Slug = "test-server",
                RebootSchedule = new RebootScheduleSettings { Enabled = false }
            };

            Assert.False(scheduler.IsDue(inst, DateTime.UtcNow));
        }

        [Fact]
        public void IsDue_WhenNotRunning_ReturnsFalse()
        {
            var runner = new TestRunner { IsRunning = false, RunningState = "Stopped" };
            var instanceService = new FakeInstanceService();
            using var scheduler = new ServerRebootSchedulerService(instanceService, runner);

            var inst = new ServerInstance
            {
                Slug = "test-server",
                RebootSchedule = new RebootScheduleSettings { Enabled = true, Mode = "Interval", IntervalHours = 1 }
            };

            Assert.False(scheduler.IsDue(inst, DateTime.UtcNow));
        }

        [Fact]
        public void IsDue_IntervalMode_CalculatesCorrectly()
        {
            var runner = new TestRunner();
            var instanceService = new FakeInstanceService();
            using var scheduler = new ServerRebootSchedulerService(instanceService, runner);

            DateTime now = DateTime.UtcNow;
            runner.SessionStart = now.AddHours(-13);

            var inst = new ServerInstance
            {
                Slug = "test-server",
                RebootSchedule = new RebootScheduleSettings { Enabled = true, Mode = "Interval", IntervalHours = 12 }
            };

            Assert.True(scheduler.IsDue(inst, now));

            // Before interval
            runner.SessionStart = now.AddHours(-6);
            Assert.False(scheduler.IsDue(inst, now));
        }

        [Fact]
        public void IsDue_DailyMode_AtTargetTime_ReturnsTrue()
        {
            var runner = new TestRunner();
            var instanceService = new FakeInstanceService();
            using var scheduler = new ServerRebootSchedulerService(instanceService, runner);

            DateTime localNow = DateTime.Today.AddHours(4).AddMinutes(5);
            DateTime utcNow = localNow.ToUniversalTime();
            runner.SessionStart = localNow.AddHours(-2).ToUniversalTime();

            var inst = new ServerInstance
            {
                Slug = "test-server",
                RebootSchedule = new RebootScheduleSettings { Enabled = true, Mode = "Daily", Time = "04:00" }
            };

            Assert.True(scheduler.IsDue(inst, utcNow));

            // Before target time
            DateTime beforeLocal = DateTime.Today.AddHours(3).AddMinutes(55);
            Assert.False(scheduler.IsDue(inst, beforeLocal.ToUniversalTime()));
        }

        [Fact]
        public void GetNextRebootTimeUtc_DailyMode_CalculatesCorrectly()
        {
            DateTime localNow = DateTime.Today.AddHours(2);
            DateTime utcNow = localNow.ToUniversalTime();

            var inst = new ServerInstance
            {
                Slug = "test-server",
                RebootSchedule = new RebootScheduleSettings { Enabled = true, Mode = "Daily", Time = "04:00" }
            };

            var next = ServerRebootSchedulerService.GetNextRebootTimeUtc(inst, utcNow);
            Assert.NotNull(next);
            Assert.Equal(DateTime.Today.AddHours(4).ToUniversalTime(), next.Value);
        }

        [Fact]
        public async Task ScheduleChange_CancelsThePendingCountdown()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"pocketmc-sched-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                var settings = new SettingsService(tempDir);
                var instanceService = new InstanceService(settings);
                var inst = await instanceService.CreateInstanceAsync("SchedServer", EngineType.Paper, "1.20.4");

                var runner = new TestRunner { IsRunning = true, RunningState = "Running", SessionStart = DateTime.UtcNow.AddHours(-2) };
                var scheduleService = new RebootScheduleService(instanceService);

                await scheduleService.SaveScheduleAsync(inst.Slug, new RebootScheduleSettings
                {
                    Enabled = true,
                    Mode = "Interval",
                    IntervalHours = 1,
                    WarningEnabled = true,
                    WarningSeconds = 60
                });

                using var scheduler = new ServerRebootSchedulerService(instanceService, runner, scheduleService);

                await scheduler.EvaluateDueInstancesAsync(DateTime.UtcNow);
                Assert.True(scheduler.IsRebootPending(inst.Slug));

                // Disabling schedule cancels pending reboot
                await scheduleService.SaveScheduleAsync(inst.Slug, new RebootScheduleSettings
                {
                    Enabled = false
                });

                Assert.False(scheduler.IsRebootPending(inst.Slug));
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }
    }
}
