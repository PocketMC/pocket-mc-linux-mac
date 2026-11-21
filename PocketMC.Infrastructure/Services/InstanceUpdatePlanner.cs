using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PocketMC.Core.Models;

namespace PocketMC.Infrastructure.Services
{
    public class InstanceUpdatePlanner
    {
        public Task<InstanceUpdatePlan> BuildPlanAsync(
            ServerInstance instance,
            string targetVersion,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(instance);

            if (string.IsNullOrWhiteSpace(targetVersion))
            {
                throw new ArgumentException("Target version must not be empty.", nameof(targetVersion));
            }

            int currentJava = 0;
            int targetJava = 0;
            string runtimeChangeText = string.Empty;

            if (instance.EngineType == EngineType.PocketMine)
            {
                runtimeChangeText = "PHP 8.2+ managed runtime";
            }
            else if (instance.EngineType == EngineType.Bedrock)
            {
                runtimeChangeText = "Native Bedrock Dedicated Server binary";
            }
            else
            {
                currentJava = MapMinecraftVersionToJavaVersion(instance.EngineVersion);
                targetJava = MapMinecraftVersionToJavaVersion(targetVersion);

                runtimeChangeText = currentJava == targetJava
                    ? $"Java {targetJava} remains required"
                    : $"Java {currentJava} -> Java {targetJava}";
            }

            string artifactName = ResolveArtifactName(instance.EngineType);

            var plan = new InstanceUpdatePlan
            {
                OperationId = Guid.NewGuid(),
                InstanceId = instance.Id,
                InstanceSlug = instance.Slug,
                InstancePath = Path.GetFullPath(instance.Path),
                CurrentEngineType = instance.EngineType,
                CurrentVersion = instance.EngineVersion,
                TargetEngineType = instance.EngineType,
                TargetVersion = targetVersion.Trim(),
                CurrentRequiredJavaVersion = currentJava,
                TargetRequiredJavaVersion = targetJava,
                RuntimeChangeText = runtimeChangeText,
                ServerArtifactFileName = artifactName
            };

            return Task.FromResult(plan);
        }

        public static int MapMinecraftVersionToJavaVersion(string mcVersion)
        {
            if (string.IsNullOrWhiteSpace(mcVersion)) return 17;

            var clean = mcVersion.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? mcVersion[1..] : mcVersion;
            var core = clean.Split(new[] { '-', '+' }, 2)[0];

            if (Version.TryParse(core, out var v))
            {
                if (v >= new Version(1, 22, 0)) return 25;
                if (v >= new Version(1, 20, 5)) return 21;
                if (v >= new Version(1, 17, 0)) return 17;
                if (v >= new Version(1, 12, 0)) return 11;
                return 8;
            }

            if (clean.StartsWith("1.22") || clean.StartsWith("1.23")) return 25;
            if (clean.StartsWith("1.21") || clean.StartsWith("1.20.5") || clean.StartsWith("1.20.6")) return 21;
            if (clean.StartsWith("1.20") || clean.StartsWith("1.19") || clean.StartsWith("1.18") || clean.StartsWith("1.17")) return 17;
            if (clean.StartsWith("1.16") || clean.StartsWith("1.15") || clean.StartsWith("1.14") || clean.StartsWith("1.13") || clean.StartsWith("1.12")) return 11;

            return 8;
        }

        public static string ResolveArtifactName(EngineType engineType)
        {
            return engineType switch
            {
                EngineType.PocketMine => "PocketMine-MP.phar",
                EngineType.Bedrock => "bedrock-server.zip",
                _ => "server.jar"
            };
        }
    }
}
