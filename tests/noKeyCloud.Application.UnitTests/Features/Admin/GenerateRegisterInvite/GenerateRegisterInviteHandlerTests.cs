using Moq;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Application.Features.Admin.GenerateRegisterInvite;
using noKeyCloud.Domain.Entities;

namespace noKeyCloud.Application.UnitTests.Features.Admin.GenerateRegisterInvite;

public class GenerateRegisterInviteHandlerTests
{
    [Fact]
    public async Task Handle_ValidRequest_CreatesInviteAndReturnsResponse()
    {
        var repoMock = new Mock<IRegisterInviteRepository>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        
        RegisterInvite? capturedInvite = null;
        repoMock.Setup(x => x.Add(It.IsAny<RegisterInvite>()))
            .Callback<RegisterInvite>(i => capturedInvite = i);

        var handler = new GenerateRegisterInviteHandler(repoMock.Object, unitOfWorkMock.Object);
        var cmd = new GenerateRegisterInviteCommand(ExpirationHours: 24);
        
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        
        Assert.False(string.IsNullOrWhiteSpace(result.Value.Code));
        Assert.True(result.Value.Code.Length <= 16);
        Assert.DoesNotContain("+", result.Value.Code);
        Assert.DoesNotContain("/", result.Value.Code);
        Assert.DoesNotContain("=", result.Value.Code);
        
        Assert.NotNull(capturedInvite);
        Assert.Equal(result.Value.Code, capturedInvite.Code);
        Assert.Equal(result.Value.Id, capturedInvite.Id);
        Assert.Equal(result.Value.CreatedAt, capturedInvite.CreatedAt);
        
        var timeDifference = capturedInvite.ExpiresAt - DateTime.UtcNow;
        Assert.True(timeDifference != null && timeDifference.Value.TotalHours > 23.9 && timeDifference.Value.TotalHours < 24.1);

        unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}