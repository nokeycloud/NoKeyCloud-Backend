namespace noKeyCloud.Contracts.Folders;

public record CreateFolderRequest(
    byte[] EncryptedName,
    byte[] EncryptedKey,
    Guid? ParentFolderId);