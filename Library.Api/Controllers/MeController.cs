using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Library.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MeController : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "MemberOrAbove")]
    public IActionResult GetMe()
    {
        var email = User.FindFirst("email")?.Value
            ?? User.FindFirst(ClaimTypes.Email)?.Value;
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();

        return Ok(new
        {
            email,
            roles
        });
    }
}
