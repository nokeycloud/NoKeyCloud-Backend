namespace noKeyCloud.Contracts.Trash;

public record GetTrashResponse
{
    public required IReadOnlyList<TrashItem> Trashes { get; init; }
}
