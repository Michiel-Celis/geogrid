using Geogrid.Api.Auth;
using Geogrid.Api.Contracts;
using Geogrid.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Geogrid.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<AppUser> _users;
    private readonly SignInManager<AppUser> _signIn;
    private readonly TokenService _tokens;

    public AuthController(UserManager<AppUser> users, SignInManager<AppUser> signIn, TokenService tokens)
    {
        _users = users;
        _signIn = signIn;
        _tokens = tokens;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        var user = new AppUser { UserName = req.Email, Email = req.Email };
        var result = await _users.CreateAsync(user, req.Password);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        var (token, expires) = _tokens.Create(user);
        return Ok(new AuthResponse(token, expires, user.Id, user.Email!));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await _users.FindByEmailAsync(req.Email);
        if (user is null) return Unauthorized();

        var ok = await _signIn.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: false);
        if (!ok.Succeeded) return Unauthorized();

        var (token, expires) = _tokens.Create(user);
        return Ok(new AuthResponse(token, expires, user.Id, user.Email!));
    }
}
