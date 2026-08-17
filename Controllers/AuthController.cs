using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PremierErp.Api.Data;
using PremierErp.Api.Models;
using PremierErp.Api.Services;

namespace PremierErp.Api.Controllers;

public record LoginDto(string Email, string Password);
public record RegisterDto(string Email, string Name, string Password, string Role);
public record ChangePwDto(string OldPassword, string NewPassword);

[ApiController]
[Route("api/auth")]
public class AuthController(ErpDbContext db, TokenService tokens) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var u = await db.Users.FirstOrDefaultAsync(x => x.Email == dto.Email.ToLower() && x.Active);
        if (u is null || !BCrypt.Net.BCrypt.Verify(dto.Password, u.PasswordHash))
            return Unauthorized(new { error = "Invalid email or password." });
        db.Audit.Add(new AuditEntry { User = u.Email, Action = "login" });
        await db.SaveChangesAsync();
        return Ok(new { token = tokens.Create(u), user = new { u.Id, u.Email, u.Name, u.Role } });
    }

    [HttpPost("register")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        var email = dto.Email.ToLower().Trim();
        if (await db.Users.AnyAsync(x => x.Email == email))
            return Conflict(new { error = "A user with this email already exists." });
        var u = new AppUser
        {
            Email = email,
            Name = dto.Name,
            Role = new[] { "admin", "manager", "user", "readonly" }.Contains(dto.Role) ? dto.Role : "user",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
        };
        db.Users.Add(u);
        await db.SaveChangesAsync();
        return Ok(new { u.Id, u.Email, u.Name, u.Role });
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePwDto dto)
    {
        var email = User.FindFirst("email")?.Value ?? User.Identity?.Name ?? "";
        var u = await db.Users.FirstOrDefaultAsync(x => x.Email == email);
        if (u is null || !BCrypt.Net.BCrypt.Verify(dto.OldPassword, u.PasswordHash))
            return Unauthorized(new { error = "Current password is incorrect." });
        u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        await db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me() => Ok(new
    {
        email = User.FindFirst("email")?.Value,
        name = User.FindFirst("name")?.Value,
        role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
    });
}
