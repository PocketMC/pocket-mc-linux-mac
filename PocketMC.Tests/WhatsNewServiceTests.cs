using PocketMC.Infrastructure.WhatsNew;
using Xunit;

namespace PocketMC.Tests
{
    public class WhatsNewServiceTests
    {
        [Fact]
        public void WhatsNewService_ShouldShow_WhenNeverSeenBefore_ReturnsTrue()
        {
            var settingsService = new FakeSettingsService();
            settingsService.Settings.LastSeenChangelogVersion = null;

            var service = new WhatsNewService(settingsService, "1.0.3");

            Assert.True(service.ShouldShow());
        }

        [Fact]
        public void WhatsNewService_MarkAsSeen_UpdatesLastSeen_AndShouldShowReturnsFalse()
        {
            var settingsService = new FakeSettingsService();
            settingsService.Settings.LastSeenChangelogVersion = null;

            var service = new WhatsNewService(settingsService, "1.0.3");
            Assert.True(service.ShouldShow());

            service.MarkAsSeen();

            Assert.Equal("1.0.3", settingsService.Settings.LastSeenChangelogVersion);
            Assert.False(service.ShouldShow());
        }

        [Fact]
        public void WhatsNewService_LoadChangelog_ParsesContentCorrectly()
        {
            var settingsService = new FakeSettingsService();
            var service = new WhatsNewService(settingsService, "1.0.3");

            string testChangelog = "VERSION=1.0.3\n\n[HIGHLIGHTS]\n- Native Linux and macOS release\n- Full Windows parity achieved\n";
            var entry = service.LoadChangelog(testChangelog);

            Assert.NotNull(entry);
            Assert.Equal("1.0.3", entry.Version);
            Assert.Single(entry.Sections);
            Assert.Equal("HIGHLIGHTS", entry.Sections[0].Name);
            Assert.Equal(2, entry.Sections[0].Items.Count);
        }
    }
}
