using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using noKeyCloud.Application.Abstractions.Services;
using noKeyCloud.Infrastructure.Options;

namespace noKeyCloud.Infrastructure.Services;

public partial class TempFileCleanupService(
    ILogger<TempFileCleanupService> logger,
    IOptions<UploadCleanupOptions> options,
    IUploadSessionStore uploadSessionStore) : BackgroundService
{
    private const string TempFileSearchPattern = "*.tmp";
    private const int DefaultAbandonedThresholdHours = 24;
    private static readonly TimeSpan StartupGracePeriod = TimeSpan.FromMinutes(15);

    private readonly UploadCleanupOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.CleanupIntervalHours <= 0)
        {
            LogServiceDisabled(_options.CleanupIntervalHours);
            return;
        }

        var thresholdHours = _options.AbandonedThresholdHours > 0
            ? _options.AbandonedThresholdHours
            : DefaultAbandonedThresholdHours;

        if (_options.AbandonedThresholdHours <= 0)
        {
            LogInvalidThreshold(_options.AbandonedThresholdHours, DefaultAbandonedThresholdHours);
        }

        var interval = TimeSpan.FromHours(_options.CleanupIntervalHours);
        var threshold = TimeSpan.FromHours(thresholdHours);

        LogStarting(interval, threshold);

        try
        {
            await Task.Delay(StartupGracePeriod, stoppingToken);

            await CleanupAsync(threshold, stoppingToken);

            using var timer = new PeriodicTimer(interval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await CleanupAsync(threshold, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            LogStopping();
        }
        catch (Exception ex)
        {
            LogUnexpectedTermination(ex);
        }
    }

    internal async Task CleanupAsync(TimeSpan threshold, CancellationToken stoppingToken)
    {
        string tempDir;
        try
        {
            tempDir = ResolveTempDirectory();
        }
        catch (InvalidOperationException ex)
        {
            LogCannotResolveTempDir(ex);
            return;
        }

        if (!Directory.Exists(tempDir))
        {
            LogTempDirDoesNotExist(tempDir);
            return;
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(tempDir, TempFileSearchPattern, SearchOption.TopDirectoryOnly);
        }
        catch (Exception ex)
        {
            LogFailedToEnumerate(ex, tempDir);
            return;
        }

        if (files.Length == 0)
        {
            LogNoFilesFound(tempDir);
            return;
        }

        LogCleanupStarted(tempDir, files.Length);

        var deleted = 0;
        var skipped = 0;
        var failed = 0;
        var cutoff = DateTime.UtcNow - threshold;

        foreach (var filePath in files)
        {
            if (stoppingToken.IsCancellationRequested) return;

            try
            {
                var info = new FileInfo(filePath);
                if (!info.Exists) continue;
                
                if (info.LastWriteTimeUtc > cutoff)
                {
                    skipped++;
                    continue;
                }

                var sessionId = ExtractSessionId(filePath);
                if (sessionId is null)
                {
                    LogSkippingNonGuid(filePath);
                    skipped++;
                    continue;
                }

                File.Delete(filePath);
                deleted++;

                try
                {
                    await uploadSessionStore.RemoveSessionAsync(sessionId.Value, stoppingToken);
                }
                catch (Exception ex)
                {
                    LogCacheRemovalFailed(ex, filePath, sessionId.Value);
                }

                LogUploadDeleted(filePath, sessionId.Value);
            }
            catch (IOException ex)
            {
                skipped++;
                LogSkippingLockedFile(ex, filePath);
            }
            catch (UnauthorizedAccessException ex)
            {
                failed++;
                LogAccessDenied(ex, filePath);
            }
            catch (Exception ex)
            {
                failed++;
                LogUnexpectedError(ex, filePath);
            }
        }

        LogCleanupFinished(deleted, skipped, failed);
    }

    private string ResolveTempDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_options.TempDirectory))
            return _options.TempDirectory;

        var basePath = Environment.GetEnvironmentVariable("BASE_UPLOAD_STORAGE_PATH");
        if (string.IsNullOrWhiteSpace(basePath))
        {
            throw new InvalidOperationException(
                "Neither UploadCleanup:TempDirectory nor BASE_UPLOAD_STORAGE_PATH is configured.");
        }

        return Path.Combine(basePath, "temp");
    }

    private static Guid? ExtractSessionId(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        return Guid.TryParse(name, out var id) ? id : null;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "TempFileCleanupService disabled: UploadCleanup:CleanupIntervalHours = {Interval}")]
    private partial void LogServiceDisabled(int interval);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid UploadCleanup:AbandonedThresholdHours={Hours}; falling back to default of {Default}h.")]
    private partial void LogInvalidThreshold(int hours, int @default);

    [LoggerMessage(Level = LogLevel.Information, Message = "TempFileCleanupService starting (interval={Interval}, threshold={Threshold}).")]
    private partial void LogStarting(TimeSpan interval, TimeSpan threshold);

    [LoggerMessage(Level = LogLevel.Information, Message = "TempFileCleanupService stopping.")]
    private partial void LogStopping();

    [LoggerMessage(Level = LogLevel.Error, Message = "TempFileCleanupService terminated unexpectedly.")]
    private partial void LogUnexpectedTermination(Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "TempFileCleanupService cannot resolve temp directory; skipping run.")]
    private partial void LogCannotResolveTempDir(Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Temp directory {Dir} does not exist; skipping cleanup.")]
    private partial void LogTempDirDoesNotExist(string dir);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to enumerate .tmp files in {Dir}.")]
    private partial void LogFailedToEnumerate(Exception ex, string dir);

    [LoggerMessage(Level = LogLevel.Debug, Message = "No .tmp files found in {Dir}.")]
    private partial void LogNoFilesFound(string dir);

    [LoggerMessage(Level = LogLevel.Information, Message = "Abandoned upload cleanup started in {Dir} ({Count} candidate file(s)).")]
    private partial void LogCleanupStarted(string dir, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Skipping {File}: filename is not a session GUID.")]
    private partial void LogSkippingNonGuid(string file);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Deleted temp file {File} but failed to remove cache session {SessionId}; continuing.")]
    private partial void LogCacheRemovalFailed(Exception ex, string file, Guid sessionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted abandoned upload {File} (session={SessionId}).")]
    private partial void LogUploadDeleted(string file, Guid sessionId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Skipping locked file {File} (possibly an active upload).")]
    private partial void LogSkippingLockedFile(Exception ex, string file);

    [LoggerMessage(Level = LogLevel.Error, Message = "Access denied deleting {File}.")]
    private partial void LogAccessDenied(Exception ex, string file);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error cleaning up {File}.")]
    private partial void LogUnexpectedError(Exception ex, string file);

    [LoggerMessage(Level = LogLevel.Information, Message = "Abandoned upload cleanup finished. Deleted={Deleted}, Skipped={Skipped}, Failed={Failed}.")]
    private partial void LogCleanupFinished(int deleted, int skipped, int failed);
}