using System.IO;
using System.Threading.Tasks;
using PocketMC.Infrastructure.Services;
using Xunit;

namespace PocketMC.Tests
{
    public class JavaProvisionerTests
    {
        [Fact]
        public async Task TestValidateJavaRuntimeWithInvalidPath()
        {
            var settingsService = new SettingsService();
            var javaService = new JavaService(settingsService);

            // Invalid path
            var result = await javaService.ValidateJavaRuntimeAsync("nonexistent_path_to_java", "21");
            Assert.False(result);
        }

        [Fact]
        public async Task TestValidateJavaRuntimeWithNonJavaBinary()
        {
            var settingsService = new SettingsService();
            var javaService = new JavaService(settingsService);

            // System binary that exits but doesn't print java version
            string nonJavaBinary = "/bin/ls";
            if (!File.Exists(nonJavaBinary))
            {
                nonJavaBinary = "/usr/bin/true";
            }

            if (File.Exists(nonJavaBinary))
            {
                var result = await javaService.ValidateJavaRuntimeAsync(nonJavaBinary, "21");
                Assert.False(result);
            }
        }
    }
}
