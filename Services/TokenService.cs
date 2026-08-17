using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using PremierErp.Api.Models;

namespace PremierErp.Api.Services;

public class TokenService(IConfiguration cfg)
{
    public string Create(AppUser u)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, u.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, u.Email),
            new Claim("name", u.Name),
            new Claim(ClaimTypes.Role, u.Role)
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!));
        var token = new JwtSecurityToken(
            issuer: cfg["Jwt:Issuer"],
            audience: cfg["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(double.Parse(cfg["Jwt:ExpiryHours"] ?? "12")),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
