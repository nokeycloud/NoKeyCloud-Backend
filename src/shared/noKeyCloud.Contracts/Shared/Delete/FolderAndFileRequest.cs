using System;
using System.Collections.Generic;
using System.Text;

namespace noKeyCloud.Contracts.Shared.Delete;

public record FolderAndFileRequest
{
    public required IReadOnlyList<Guid> FileIds { get; init; }
    public required IReadOnlyList<Guid> FolderIds { get; init; }
    public required byte[] EncryptedKey { get; init; }
}
