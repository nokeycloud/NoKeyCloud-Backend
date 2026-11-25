namespace noKeyCloud.Contracts.Trash;

public record TrashItemsRequest
{
    public required IReadOnlyList<Guid> FileIds { get; init; }
    public required IReadOnlyList<Guid> FolderIds { get; init; }
    public required byte[] EncryptedKey { get; init; }
}
