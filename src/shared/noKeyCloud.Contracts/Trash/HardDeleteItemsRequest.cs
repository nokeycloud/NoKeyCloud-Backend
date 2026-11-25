using System;
using System.Collections.Generic;
using System.Text;

namespace noKeyCloud.Contracts.Trash;
public record HardDeleteItemsRequest
{
    public required IReadOnlyList<Guid> FileIds { get; init; }
    public required IReadOnlyList<Guid> FolderIds { get; init; }
}
