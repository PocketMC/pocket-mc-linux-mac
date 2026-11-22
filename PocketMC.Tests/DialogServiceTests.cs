using System;
using System.Threading.Tasks;
using PocketMC.Core.Services;
using Xunit;

namespace PocketMC.Tests;

public class DialogServiceTests
{
    [Theory]
    [InlineData(DialogType.Information, false, "OK", null)]
    [InlineData(DialogType.Question, false, "Yes", "No")]
    [InlineData(DialogType.Warning, true, "OK", "Cancel")]
    [InlineData(DialogType.Error, true, "OK", "Cancel")]
    public void ResolveDefaultButtons_ReturnsExpectedLabels(DialogType type, bool showCancel, string expectedPrimary, string? expectedSecondary)
    {
        var (primary, secondary) = DialogButtonResolver.Resolve(type, showCancel, null, null);
        Assert.Equal(expectedPrimary, primary);
        Assert.Equal(expectedSecondary, secondary);
    }
}
