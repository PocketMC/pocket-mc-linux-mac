using System;
using System.IO;
using System.Threading.Tasks;
using PocketMC.Core.Services;
using PocketMC.Platform.Lifecycle;
using Xunit;

namespace PocketMC.Tests;

public class SingleInstanceServiceTests
{
    [Fact]
    public void SocketPath_IsDeterministicAndNonEmpty()
    {
        string path = SingleInstanceService.GetDefaultSocketPath();
        Assert.False(string.IsNullOrWhiteSpace(path));
        Assert.True(path.Contains("pocketmc", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SingleInstance_FirstInstanceAcquiresLock_SecondForwardsArguments()
    {
        string testSocket = Path.Combine(Path.GetTempPath(), $"pmc_test_{Guid.NewGuid():N}.sock");
        var tcs = new TaskCompletionSource<string[]>(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            using var instance1 = new SingleInstanceService(testSocket);
            bool locked1 = instance1.TryAcquireLock();
            Assert.True(locked1);
            Assert.True(instance1.IsFirstInstance);

            instance1.StartListening(args => tcs.TrySetResult(args));

            using var instance2 = new SingleInstanceService(testSocket);
            bool locked2 = instance2.TryAcquireLock();
            Assert.False(locked2);
            Assert.False(instance2.IsFirstInstance);

            string[] payload = new[] { "pocketmc://open", "--minimized" };
            bool sent = await instance2.SendArgumentsToExistingInstanceAsync(payload);
            Assert.True(sent);

            var received = await Task.WhenAny(tcs.Task, Task.Delay(3000));
            Assert.Same(tcs.Task, received);
            Assert.Equal(payload, await tcs.Task);
        }
        finally
        {
            if (File.Exists(testSocket))
            {
                try { File.Delete(testSocket); } catch { }
            }
        }
    }
}
