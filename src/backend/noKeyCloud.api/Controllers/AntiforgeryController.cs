using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace noKeyCloud.api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AntiforgeryController(IAntiforgery antiforgery) : ControllerBase
{
    [HttpGet("token")]
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult GetToken()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { token = tokens.RequestToken });
    }
}

