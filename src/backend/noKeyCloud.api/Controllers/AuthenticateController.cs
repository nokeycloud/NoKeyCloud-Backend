using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using noKeyCloud.Application.Features.Users.LoginInit;
using noKeyCloud.Application.Features.Users.LoginVerify;
using noKeyCloud.Application.Features.Users.Logout;
using noKeyCloud.Application.Features.Users.RefreshSession;
using noKeyCloud.Application.Features.Users.Register;
using noKeyCloud.Application.Features.Users.RemoveUser;
using noKeyCloud.Contracts.Authenticate;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using noKeyCloud.Application.Configuration;
using noKeyCloud.Application.Features.Users.RegisterInit;

namespace noKeyCloud.api.Controllers;


[ApiController]
[Route("api/[controller]")]
public class AuthenticateController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly JwtOptions _jwtOptions;
    
    public AuthenticateController(IMediator mediator, IOptions<JwtOptions> jwtOptions)
    {
        _mediator = mediator;
        _jwtOptions = jwtOptions.Value;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterUserRequest request)
    {
        var command = new RegisterUserCommand(
            request.UserIdentifier,
            request.Username,
            request.Email,
            request.RegistrationRecord,
            request.EncryptedMasterKey,
            request.KeySalt,
            request.RecoveryEncryptedMasterKey,
            request.RootFolderKey,
            request.RegisterInviteCode);

        var result = await _mediator.Send(command);
        if (result.IsSuccess)
        {
            return Ok();
        }
        return BadRequest(result.Error);
    }

    [HttpPost("login/init")]
    public async Task<IActionResult> InitLogin([FromBody] LoginInitRequest request)
    {
        var command = new LoginInitCommand(
            request.Username,
            request.Email,
            request.StartLoginRequest);

        var result = await _mediator.Send(command);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return BadRequest(result.Error);
    }

    [HttpPost("login/verify")]
    public async Task<IActionResult> VerifyLogin([FromBody] LoginVerifyRequest request)
    {
        var command = new LoginVerifyCommand(
            request.SessionId,
            request.FinishLoginRequest);

        var result = await _mediator.Send(command);

        if (result.IsSuccess)
        {
            Response.Cookies.Append("access_token", result.Value!.JwtToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/",
                Expires = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenLifetimeMinutes)
            });

            Response.Cookies.Append("refresh_token", result.Value!.RefreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/",
                Expires = DateTime.UtcNow.AddHours(_jwtOptions.RefreshTokenLifetimeHours)
            });

            return Ok(result.Value!.ResponsePayload);
        }
        else if (result.Error == "Invalid credentials" || result.Error == "Session not found." || result.Error == "Login verification failed")
        {
            return Unauthorized(result.Error);
        }

        return BadRequest(result.Error);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshSession([FromBody] RefreshSessionRequest request)
    {
        if (!Request.Cookies.TryGetValue("refresh_token", out var refreshTokenCookie) || string.IsNullOrEmpty(refreshTokenCookie))
        {
            return Unauthorized("Refresh token is missing.");
        }

        var command = new RefreshSessionCommand(refreshTokenCookie);
        var result = await _mediator.Send(command);

        if (!result.IsSuccess)
        {
            return Unauthorized(result.Error);
        }

        Response.Cookies.Append("access_token", result.Value!.JwtToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenLifetimeMinutes)
        });

        Response.Cookies.Append("refresh_token", result.Value!.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = DateTime.UtcNow.AddHours(_jwtOptions.RefreshTokenLifetimeHours)
        });

        return Ok(result.Value!.ResponsePayload);
    }


    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> RemoveUser([FromRoute] Guid id, RemoveUserRequest request)
    {
        var command = new RemoveUserCommand(
            id);

        var result = await _mediator.Send(command);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return BadRequest(result.Error);
    }


    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var userIdClaim = User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized("User context is invalid.");
        }

        Request.Cookies.TryGetValue("refresh_token", out var refreshTokenCookie);

        var command = new LogoutUserCommand(userId, refreshTokenCookie ?? "");

        var result = await _mediator.Send(command);

        var cookieDeletionOptions = new CookieOptions
        {
            Path = "/",
            Secure = true,
            HttpOnly = true,
            SameSite = SameSiteMode.Strict
        };

        Response.Cookies.Delete("access_token", cookieDeletionOptions);
        Response.Cookies.Delete("refresh_token", cookieDeletionOptions);

        if (result.IsSuccess)
        {
            return Ok();
        }

        if (result.Error.Contains("Token.NotFound"))
        {
            return Ok();
        }

        return BadRequest(result.Error);
    }

    [HttpPost("register/init")]
    public async Task<IActionResult> RegisterInit([FromBody] RegisterResponseUserRequest request)
    {
        var command = new RegisterResponseUserCommand(
            request.UserIdentifier,
            request.RegistrationRequest);
        
        var result = await _mediator.Send(command);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }
        
        return BadRequest(result.Error);
    }
}