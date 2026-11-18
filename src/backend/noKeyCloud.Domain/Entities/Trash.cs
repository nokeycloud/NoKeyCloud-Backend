using System;
using noKeyCloud.Contracts.Shared;

namespace noKeyCloud.Domain.Entities;

public class Trash
{
    public Guid Id { get; private set; }
    public byte[] EncryptedName { get; private set; }
    public long SizeBytes { get; private set; }
    public DateTime DeletedAt { get; private set; }
    public TrashType Type { get; private set; }
    public byte[] EncryptedKey { get; private set; }
    public Guid UserId { get; private set; }

    public Trash(Guid id, byte[] encryptedName, long sizeBytes,
                 DateTime deletedAt, TrashType type, byte[] encryptedKey, Guid userId)
    {
        Id = id;
        EncryptedName = encryptedName;
        SizeBytes = sizeBytes;
        DeletedAt = deletedAt;
        Type = type;             
        EncryptedKey = encryptedKey;
        UserId = userId;

    }
}