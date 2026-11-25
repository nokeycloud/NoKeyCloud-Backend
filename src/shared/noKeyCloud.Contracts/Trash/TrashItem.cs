namespace noKeyCloud.Contracts.Trash;

public record TrashItem(
    Guid Id,
    byte[] EncryptedName,
    long SizeBytes,
    TrashType Type,
    byte[] EncryptedKey,
    DateTime DeletedAt,
    Guid UserId);
