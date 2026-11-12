using System;

namespace noKeyCloud.Domain.Entities;

public enum TrashType
{
    FileId,
    FolderId
}

public class Trash
{
    public Guid Id { get; private set; }
    public string EncryptedName { get; private set; }
    public byte[] SizeBytes { get; private set; }
    public DateTime DeletedAt { get; private set; }
    public TrashType Type { get; private set; }
    public string EncryptedKey { get; private set; }

    private Trash() { }

    public Trash(Guid id, string encryptedName, byte[] sizeBytes,
                 DateTime deletedAt, TrashType type, string encryptedKey)
    {
        Id = id;
        EncryptedName = encryptedName;
        SizeBytes = sizeBytes;
        DeletedAt = deletedAt;   
        Type = type;             
        EncryptedKey = encryptedKey;
    }
}