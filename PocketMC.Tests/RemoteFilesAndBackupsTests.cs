using PocketMC.RemoteControl.Models;
using Xunit;

namespace PocketMC.RemoteControl.Tests;

public class RemoteFilesAndBackupsTests
{
    [Fact]
    public void RemoteFileItemDto_DefaultProperties_AreSetCorrectly()
    {
        var dto = new RemoteFileItemDto
        {
            Name = "server.properties",
            RelativePath = "server.properties",
            IsDirectory = false,
            SizeBytes = 1024,
            Extension = ".properties"
        };

        Assert.Equal("server.properties", dto.Name);
        Assert.Equal("server.properties", dto.RelativePath);
        Assert.False(dto.IsDirectory);
        Assert.Equal(1024, dto.SizeBytes);
        Assert.Equal(".properties", dto.Extension);
    }

    [Fact]
    public void RemoteBackupDto_DefaultProperties_AreSetCorrectly()
    {
        var backup = new RemoteBackupDto
        {
            Id = "manual-backup-2026.zip",
            FileName = "manual-backup-2026.zip",
            SizeBytes = 5242880,
            Type = "Local",
            IsAutomated = false
        };

        Assert.Equal("manual-backup-2026.zip", backup.Id);
        Assert.Equal(5242880, backup.SizeBytes);
        Assert.Equal("Local", backup.Type);
        Assert.False(backup.IsAutomated);
    }

    [Fact]
    public void RemoteFileContentDto_TextFileProperties_SetCorrectly()
    {
        var content = new RemoteFileContentDto
        {
            RelativePath = "eula.txt",
            Content = "eula=true",
            IsText = true,
            SizeBytes = 9
        };

        Assert.Equal("eula.txt", content.RelativePath);
        Assert.Equal("eula=true", content.Content);
        Assert.True(content.IsText);
        Assert.False(content.IsTruncated);
    }

    [Fact]
    public void SaveRemoteFileContentRequest_Properties_SetCorrectly()
    {
        var req = new SaveRemoteFileContentRequest
        {
            RelativePath = "config/paper-global.yml",
            Content = "settings: {}"
        };

        Assert.Equal("config/paper-global.yml", req.RelativePath);
        Assert.Equal("settings: {}", req.Content);
    }

    [Theory]
    [InlineData("server.properties", false, true)]
    [InlineData("config/paper.yml", false, true)]
    [InlineData(".env", false, false)]
    [InlineData(".env", true, true)]
    [InlineData("id_rsa", false, false)]
    [InlineData("forwarding.secret", false, false)]
    [InlineData("forwarding.secret", true, true)]
    [InlineData("server~1.pro", false, false)]
    [InlineData(".pocketmc-bridge/token", false, false)]
    public void RemoteFileAccessPolicy_ValidatesPathCorrectly(string path, bool isAdmin, bool expected)
    {
        bool allowed = PocketMC.RemoteControl.Services.RemoteFileAccessPolicy.CanAccess(path, isAdmin);
        Assert.Equal(expected, allowed);
    }
}