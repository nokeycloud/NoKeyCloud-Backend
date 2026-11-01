namespace noKeyCloud.Infrastructure.Options;

public sealed class UploadCleanupOptions
{
    public const string SectionName = "UploadCleanup";
    public int CleanupIntervalHours { get; set; } = 1;
    public int AbandonedThresholdHours { get; init; } = 24;
    public string? TempDirectory { get; init; }
}