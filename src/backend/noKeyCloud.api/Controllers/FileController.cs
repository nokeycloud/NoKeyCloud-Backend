using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using noKeyCloud.Application.Features.Files.CreateFile;
using noKeyCloud.Application.Features.Files.UploadFile;
using noKeyCloud.Contracts.File;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using noKeyCloud.Application.Features.Shared;
using noKeyCloud.Application.Features.Files.ListTrashFiles;
using noKeyCloud.Contracts.Shared.Delete;

namespace noKeyCloud.api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FileController : ControllerBase
{
    private readonly IMediator _mediator;

    public FileController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [Authorize]
    [HttpPost("createFile")]
    public async Task<IActionResult> CreateFile([FromBody] CreateFileRequest request)
    {
        var userIdClaim = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized("Invalid token claims");
        }


        var command = new CreateFileCommand(
            userId,
            request.FileName,
            request.MimeType,
            request.EncryptedKey,
            request.Checksum,
            request.FolderId);

        var result = await _mediator.Send(command);

        if (result.IsSuccess)
        {
            return Ok();
        }
        return BadRequest(result.Error);
    }

    [Authorize]
    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile([FromBody] UploadFileRequest request)
    {
        var userIdClaim = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized("Invalid token claims");
        }

        var command = new UploadFileCommand(
            userId,
            request.FileName,
            request.MimeType,
            request.SizeBytes,
            request.EncryptedKey,
            request.Checksum,
            request.FolderId,
            request.FileContent);

        var result = await _mediator.Send(command);

        if (result.IsSuccess)
        {
            return Ok();
        }
        return BadRequest(result.Error);
    }
    [Authorize]
    [HttpGet("{id}")]
    public async Task<IActionResult> GetFile([FromRoute] Guid id)
    {
        var userIdClaim = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized("Invalid token claims");
        }
        var command = new DownloadFileCommand
            (
            userId,
            id);

        var result = await _mediator.Send(command);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }
        return BadRequest(result.Error);
    }
    
    [Authorize]
    [HttpPost("trash")]
    public async Task<IActionResult> SoftDeleteFile([FromBody] FolderAndFileRequest request)
    {
        var userIdClaim = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized("Invalid token claims");
        }
        
        var command = new SoftDeleteFileCommand(
            FileIds: request.FileIds,
            FolderIds: request.FolderIds,
            userId: userId,
            encryptedKey: request.EncryptedKey);

        var result = await _mediator.Send(command);

        if (result.IsSuccess)
        {
            return Ok();
        }
        return BadRequest(result.Error);
    }
    [Authorize]
    [HttpGet("trash")]
    public async Task<IActionResult> FileTrash()
    {
        var userIdClaim = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized("Invalid token claims");
        }

        var query = new ListTrashFilesQuery(userId);

        var result = await _mediator.Send(query);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }
        return BadRequest(result.Error);
    }
}
