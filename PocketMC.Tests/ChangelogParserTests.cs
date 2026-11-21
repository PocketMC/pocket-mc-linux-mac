using System.Linq;
using PocketMC.Infrastructure.WhatsNew;
using Xunit;

namespace PocketMC.Tests
{
    public class ChangelogParserTests
    {
        [Fact]
        public void ChangelogParser_Parse_ValidContent_ExtractsVersionAndSections()
        {
            string rawContent = "VERSION=1.9.10\n\n[NEW FEATURES]\n- Read PocketMC announcements in the new News page.\n- Added Crossplay support.\n\n[FIXES & STABILITY]\n- Fixed window maximization state on restore.\n";

            var entry = ChangelogParser.Parse(rawContent);

            Assert.NotNull(entry);
            Assert.Equal("1.9.10", entry.Version);
            Assert.Equal(2, entry.Sections.Count);

            var features = entry.Sections[0];
            Assert.Equal("NEW FEATURES", features.Name);
            Assert.Equal(2, features.Items.Count);
            Assert.Equal("Read PocketMC announcements in the new News page.", features.Items[0]);
            Assert.Equal("Added Crossplay support.", features.Items[1]);

            var fixes = entry.Sections[1];
            Assert.Equal("FIXES & STABILITY", fixes.Name);
            Assert.Single(fixes.Items);
            Assert.Equal("Fixed window maximization state on restore.", fixes.Items[0]);
        }
    }
}
