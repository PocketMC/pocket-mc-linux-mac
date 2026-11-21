using System;
using System.IO;
using System.Threading.Tasks;
using PocketMC.Core.Models;
using PocketMC.Infrastructure.Services;
using Xunit;

namespace PocketMC.Tests
{
    public class RebootScheduleServiceTests
    {
        [Fact]
        public async Task SaveScheduleAsync_ValidatesInputParameters()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"pocketmc-schedule-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                var settingsService = new SettingsService(tempDir);
                var instanceService = new InstanceService(settingsService);
                var instance = await instanceService.CreateInstanceAsync("ScheduleServer", EngineType.Paper, "1.20.4");
                var scheduleService = new RebootScheduleService(instanceService);

                // Invalid mode
                await Assert.ThrowsAsync<ArgumentException>(async () =>
                {
                    await scheduleService.SaveScheduleAsync(instance.Slug, new RebootScheduleSettings
                    {
                        Enabled = true,
                        Mode = "InvalidMode"
                    });
                });

                // Invalid time format
                await Assert.ThrowsAsync<ArgumentException>(async () =>
                {
                    await scheduleService.SaveScheduleAsync(instance.Slug, new RebootScheduleSettings
                    {
                        Enabled = true,
                        Mode = "Daily",
                        Time = "25:99"
                    });
                });

                // Invalid interval
                await Assert.ThrowsAsync<ArgumentException>(async () =>
                {
                    await scheduleService.SaveScheduleAsync(instance.Slug, new RebootScheduleSettings
                    {
                        Enabled = true,
                        Mode = "Interval",
                        IntervalHours = 0
                    });
                });

                // Valid daily schedule saves and can be retrieved
                var validDaily = new RebootScheduleSettings
                {
                    Enabled = true,
                    Mode = "Daily",
                    Time = "05:30",
                    WarningEnabled = true,
                    WarningSeconds = 45
                };

                await scheduleService.SaveScheduleAsync(instance.Slug, validDaily);

                var retrieved = scheduleService.GetSchedule(instance.Slug);
                Assert.NotNull(retrieved);
                Assert.True(retrieved.Enabled);
                Assert.Equal("Daily", retrieved.Mode);
                Assert.Equal("05:30", retrieved.Time);
                Assert.Equal(45, retrieved.WarningSeconds);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }
    }
}
