using noKeyCloud.Contracts.Shared;

namespace noKeyCloud.Contracts.Shared.GetFileAndFolder;

public record TrashItem(
    Guid Id,
    byte[] EncryptedName,
    long SizeBytes,
    TrashType Type,
    byte[] EncryptedKey,
    DateTime DeletedAt,
    Guid UserId);

public record GetFileAndFolderResponse
{
    public required IReadOnlyList<TrashItem> Trashes { get; init; }
}
