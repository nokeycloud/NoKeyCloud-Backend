using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using noKeyCloud.Application.Features.Trash.HardDeleteFiles;
using noKeyCloud.Application.Features.Trash.ListTrash;
using noKeyCloud.Application.Features.Trash.TrashItems;
using noKeyCloud.Contracts.Trash;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace noKeyCloud.api.Controllers;

[ApiController]
[Route("api/[controller]")]

public class TrashController : ControllerBase
{
    private readonly IMediator _mediator;
    public TrashController(IMediator mediator)
    {
        _mediator = mediator;
    }
    [Authorize]
    [HttpPost("trash")]
    public async Task<IActionResult> TrashItems([FromBody] TrashItemsRequest request)
    {
        var userIdClaim = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized("Invalid token claims");
        }

        var command = new TrashItemsCommand(
            FileIds: request.FileIds,
            FolderIds: request.FolderIds,
            UserId: userId,
            EncryptedKey: request.EncryptedKey);

        var result = await _mediator.Send(command);

        if (result.IsSuccess)
        {
            return Ok();
        }
        return BadRequest(result.Error);
    }
    [Authorize]
    [HttpGet("trash")]
    public async Task<IActionResult> ListTrash()
    {
        var userIdClaim = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized("Invalid token claims");
        }

        var query = new ListTrashQuery(userId);

        var result = await _mediator.Send(query);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }
        return BadRequest(result.Error);
    }
    [Authorize]
    [HttpPost("trash")]
    public async Task<IActionResult> TrashItems([FromBody] HardDeleteItemsRequest request)
    {
        var userIdClaim = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized("Invalid token claims");
        }

        var command = new HardDeleteFilesCommand(
            FileIds: request.FileIds,
            FolderIds: request.FolderIds,
            UserId: userId
            );

        var result = await _mediator.Send(command);

        if (result.IsSuccess)
        {
            return Ok();
        }
        return BadRequest(result.Error);
    }
}

