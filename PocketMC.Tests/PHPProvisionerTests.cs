using System;
using System.IO;
using System.IO.Compression;
using PocketMC.Infrastructure.Utils;
using Xunit;

namespace PocketMC.Tests
{
    public class PHPProvisionerTests
    {
        [Fact]
        public void TestZipExtractorZipSlipThrows()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "PocketMC_Test_Extract_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);
            
            var zipPath = Path.Combine(tempDir, "malicious.zip");

            try
            {
                // Create a malicious zip file
                using (var fs = new FileStream(zipPath, FileMode.Create))
                using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    // Create an entry that attempts path traversal outside destination
                    var entry = archive.CreateEntry("../outside.txt");
                    using (var writer = new StreamWriter(entry.Open()))
                    {
                        writer.Write("malicious content");
                    }
                }

                // Verify that attempting to extract throws InvalidOperationException
                Assert.Throws<InvalidOperationException>(() => SafeZipExtractor.ExtractZip(zipPath, tempDir));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
    }
}
