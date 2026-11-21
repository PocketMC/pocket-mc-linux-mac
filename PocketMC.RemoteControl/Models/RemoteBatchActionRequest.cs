namespace PocketMC.RemoteControl.Models;

public sealed class RemoteBatchActionRequest
{
    public string Action { get; set; } = string.Empty;
    public List<Guid> InstanceIds { get; set; } = new();
}

public sealed record RemoteBatchItemResult(Guid InstanceId, string Status, string? Message = null);
