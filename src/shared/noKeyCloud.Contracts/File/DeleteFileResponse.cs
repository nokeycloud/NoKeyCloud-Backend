using System;
using System.Collections.Generic;
using System.Text;

namespace noKeyCloud.Contracts.File;
public record DeleteFileResponse
{
    public Boolean Success { get; init; }
}
