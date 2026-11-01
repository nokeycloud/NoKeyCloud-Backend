using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using noKeyCloud.Application.Abstractions.Services;
using noKeyCloud.Infrastructure.Options;
using noKeyCloud.Infrastructure.Services;
using SysFile = System.IO.File;

namespace noKeyCloud.Application.UnitTests.Features.Services;

public class TempFileCleanupServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly Mock<IUploadSessionStore> _uploadSessionStoreMock;
    private readonly TempFileCleanupService _service;

    public TempFileCleanupServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "noKeyCloud_CleanupTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDirectory);

        _uploadSessionStoreMock = new Mock<IUploadSessionStore>();

        var options = Options.Create(new UploadCleanupOptions
        {
            TempDirectory = _tempDirectory,
            AbandonedThresholdHours = 24
        });

        _service = new TempFileCleanupService(
            NullLogger<TempFileCleanupService>.Instance,
            options,
            _uploadSessionStoreMock.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    [Fact]
    public async Task CleanupAsync_DeletesAbandonedFiles_AndRemovesSessions()
    {
        var sessionId = Guid.NewGuid();
        var filePath = Path.Combine(_tempDirectory, $"{sessionId}.tmp");
        
        await SysFile.WriteAllTextAsync(filePath, "chunk data", TestContext.Current.CancellationToken);
        SysFile.SetLastWriteTimeUtc(filePath, DateTime.UtcNow.AddHours(-25));
        
        await _service.CleanupAsync(TimeSpan.FromHours(24), CancellationToken.None);
        
        Assert.False(SysFile.Exists(filePath));
        _uploadSessionStoreMock.Verify(
            x => x.RemoveSessionAsync(sessionId, It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task CleanupAsync_SkipsActiveFiles_WithinThreshold()
    {
        var sessionId = Guid.NewGuid();
        var filePath = Path.Combine(_tempDirectory, $"{sessionId}.tmp");
        await SysFile.WriteAllTextAsync(filePath, "chunk data", TestContext.Current.CancellationToken);
        
        await _service.CleanupAsync(TimeSpan.FromHours(24), CancellationToken.None);

        Assert.True(SysFile.Exists(filePath));
        _uploadSessionStoreMock.Verify(
            x => x.RemoveSessionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), 
            Times.Never);
    }

    [Fact]
    public async Task CleanupAsync_HandlesNonGuidFiles_Gracefully()
    {
        var filePath = Path.Combine(_tempDirectory, "not-a-guid.tmp");
        await SysFile.WriteAllTextAsync(filePath, "chunk data", TestContext.Current.CancellationToken);
        SysFile.SetLastWriteTimeUtc(filePath, DateTime.UtcNow.AddHours(-25));

        await _service.CleanupAsync(TimeSpan.FromHours(24), CancellationToken.None);

        Assert.True(SysFile.Exists(filePath));
    }

    [Fact]
    public async Task CleanupAsync_HandlesExceptionsDuringSessionRemoval_Gracefully()
    {
        var sessionId1 = Guid.NewGuid();
        var sessionId2 = Guid.NewGuid();
        var filePath1 = Path.Combine(_tempDirectory, $"{sessionId1}.tmp");
        var filePath2 = Path.Combine(_tempDirectory, $"{sessionId2}.tmp");
        
        await SysFile.WriteAllTextAsync(filePath1, "data1", TestContext.Current.CancellationToken);
        await SysFile.WriteAllTextAsync(filePath2, "data2", TestContext.Current.CancellationToken);
        SysFile.SetLastWriteTimeUtc(filePath1, DateTime.UtcNow.AddHours(-25));
        SysFile.SetLastWriteTimeUtc(filePath2, DateTime.UtcNow.AddHours(-25));
        
        _uploadSessionStoreMock
            .Setup(x => x.RemoveSessionAsync(sessionId1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Simulated Cache Failure"));
            
        _uploadSessionStoreMock
            .Setup(x => x.RemoveSessionAsync(sessionId2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        
        var exception = await Record.ExceptionAsync(() => 
            _service.CleanupAsync(TimeSpan.FromHours(24), CancellationToken.None));

        Assert.Null(exception); 

        Assert.False(SysFile.Exists(filePath1));
        Assert.False(SysFile.Exists(filePath2));
        
        _uploadSessionStoreMock.Verify(x => x.RemoveSessionAsync(sessionId1, It.IsAny<CancellationToken>()), Times.Once);
        _uploadSessionStoreMock.Verify(x => x.RemoveSessionAsync(sessionId2, It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task CleanupAsync_DoesNotThrowWhenDirectoryIsMissing()
    {
        var missingDir = Path.Combine(_tempDirectory, "missing_sub_dir");
        var options = Options.Create(new UploadCleanupOptions { TempDirectory = missingDir });
        
        var service = new TempFileCleanupService(
            NullLogger<TempFileCleanupService>.Instance,
            options,
            _uploadSessionStoreMock.Object);
        
        var exception = await Record.ExceptionAsync(() => 
            service.CleanupAsync(TimeSpan.FromHours(24), CancellationToken.None));
        
        Assert.Null(exception);
    }
}