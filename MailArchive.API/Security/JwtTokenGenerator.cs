using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MailArchive.Application.Auth;
using MailArchive.Domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace MailArchive.API.Security;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly IConfiguration _configuration;

    public JwtTokenGenerator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public JwtTokenResult GenerateToken(User user)
    {
        var key = _configuration["Jwt:Key"];

        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("JWT key is not configured.");

        var issuer = _configuration["Jwt:Issuer"];
        var audience = _configuration["Jwt:Audience"];

        var expiresMinutesValue = _configuration["Jwt:ExpiresMinutes"];

        var expiresMinutes = int.TryParse(expiresMinutesValue, out var parsedExpiresMinutes)
            ? parsedExpiresMinutes
            : 60;

        var expiresAtUtc = DateTime.UtcNow.AddMinutes(expiresMinutes);

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));

        var credentials = new SigningCredentials(
            securityKey,
            SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        return new JwtTokenResult(accessToken, expiresAtUtc);
    }
}