using System;

namespace PocketMC.Core.Models.Networking;

public sealed class PortReliabilityException : Exception
{
    public PortReliabilityException(PortCheckResult result)
        : base(result?.FailureMessage ?? $"Port validation failed with code {result?.FailureCode}.")
    {
        Result = result ?? throw new ArgumentNullException(nameof(result));
    }

    public PortReliabilityException(string message, PortCheckResult? result = null)
        : base(message)
    {
        Result = result;
    }

    public PortReliabilityException(string message, Exception innerException, PortCheckResult? result = null)
        : base(message, innerException)
    {
        Result = result;
    }

    public PortCheckResult? Result { get; }
}
