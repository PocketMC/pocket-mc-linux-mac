using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PocketMC.Core.Models;

namespace PocketMC.Core.Services;

public interface IRefreshableServerSoftwareProvider
{
    string DisplayName { get; }
    DateTimeOffset? LastSuccessfulRefreshUtc { get; }
    string? LastRefreshError { get; }
    Task<List<MinecraftVersion>> GetAvailableVersionsAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);
}
