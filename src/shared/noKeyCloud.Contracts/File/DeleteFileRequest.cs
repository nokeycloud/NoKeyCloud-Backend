using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace noKeyCloud.Contracts.File;
public record DeleteFileRequest
{
    public required List<Guid> FileListId { get;init; }
};