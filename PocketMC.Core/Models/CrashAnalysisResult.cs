namespace PocketMC.Core.Models;

public enum CrashCategory
{
    None = 0,
    ModDependency,
    MissingMod,
    MixinConflict,
    OutOfMemory,
    JavaRuntime,
    BedrockScript,
    PocketMineFatal,
    ServerTickException,
    StartupAborted,
    ProcessExitError,
    Unknown
}

public class CrashAnalysisResult
{
    public bool IsCrash { get; set; }
    public CrashCategory Category { get; set; } = CrashCategory.None;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string FullLogContext { get; set; } = string.Empty;
    public string? CrashReportPath { get; set; }
    public int ExitCode { get; set; }

    public bool IsFatalModConfigurationError =>
        Category is CrashCategory.ModDependency
                 or CrashCategory.MissingMod
                 or CrashCategory.MixinConflict;

    public static CrashAnalysisResult CleanExit(int exitCode = 0) => new()
    {
        IsCrash = false,
        Category = CrashCategory.None,
        ExitCode = exitCode
    };
}
