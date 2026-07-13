# Phase 2: Instance Lifecycle & Console - Pattern Map

This document lists the existing code analogs and structural patterns identified in the PocketMC codebase to guide the implementation of Phase 2.

---

## 1. Service Interface & Implementation Separation

### Pattern
PocketMC follows a strict clean architecture pattern separating interfaces from implementations across projects:
- **Core Interfaces:** Defined in `PocketMC.Core/Services/` namespace `PocketMC.Core.Services`.
- **Infrastructure Implementations:** Defined in `PocketMC.Infrastructure/Services/` namespace `PocketMC.Infrastructure.Services` (for cross-platform general services).
- **Platform-Specific Implementations:** Defined in `PocketMC.Platform/Services/` namespace `PocketMC.Platform.Services` (for OS-specific logic such as secret stores).

### Reusable Analogs

#### Interface Pattern: `ISettingsService.cs`
Located at `PocketMC.Core/Services/ISettingsService.cs`:
```csharp
namespace PocketMC.Core.Services
{
    public interface ISettingsService
    {
        string GetSettingsDirectory();
        string GetInstancesDirectory();
        // ...
    }
}
```

#### Implementation Pattern: `SettingsService.cs`
Located at `PocketMC.Infrastructure/Services/SettingsService.cs`:
```csharp
using PocketMC.Core.Services;

namespace PocketMC.Infrastructure.Services
{
    public class SettingsService : ISettingsService
    {
        // ...
    }
}
```

---

## 2. Dependency Injection Registration

All services are registered as Singletons in the DI container inside the main program startup.

### Analog: `Program.cs`
Located at `PocketMC.App/Program.cs`:
```csharp
public static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .ConfigureServices((hostContext, services) =>
        {
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<ISecretStore>(sp => 
                SecretStoreFactory.Create(sp.GetRequiredService<ISettingsService>()));
            services.AddSingleton<IJavaService, JavaService>();
            services.AddSingleton<IPHPService, PHPService>();
            services.AddSingleton<IPreLaunchVerifier, PreLaunchVerifier>();
        });
```

---

## 3. Platform OS Detection and P/Invoke

For platform-specific secure storage resolution, PocketMC uses runtime checks. We will replicate this pattern to select process signal handlers or Unix native calls.

### Analog: `SecretStoreFactory.cs`
Located at `PocketMC.Platform/Services/SecretStoreFactory.cs`:
```csharp
using System.Runtime.InteropServices;

public static class SecretStoreFactory
{
    public static ISecretStore Create(ISettingsService settingsService)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return new MacKeychainSecretStore();
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new LinuxSecretServiceSecretStore();
        }
        return new AesFallbackSecretStore(settingsService);
    }
}
```
We will follow this factory or structural check pattern if any OS-specific shell wrappers or process adjustments are needed.
