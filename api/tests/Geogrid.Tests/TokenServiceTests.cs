using System.IdentityModel.Tokens.Jwt;
using Geogrid.Api.Auth;
using Geogrid.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Geogrid.Tests;

public class TokenServiceTests
{
    [Fact]
    public void Create_ProducesValidJwt_WithSubAndEmail()
    {
        var opts = Options.Create(new JwtOptions
        {
            Issuer = "geogrid-test",
            Audience = "geogrid-test",
            Key = "test-key-must-be-at-least-32-characters-long",
            ExpiresMinutes = 60
        });
        var svc = new TokenService(opts);

        var user = new AppUser { Id = Guid.NewGuid(), Email = "u@example.com", UserName = "u@example.com" };
        var (token, expires) = svc.Create(user);

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.True(expires > DateTimeOffset.UtcNow);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("geogrid-test", jwt.Issuer);
        Assert.Contains(jwt.Claims, c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == user.Id.ToString());
        Assert.Contains(jwt.Claims, c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "u@example.com");
    }
}
