namespace Geogrid.Api.Auth;

public class JwtOptions
{
    public string Issuer { get; set; } = "geogrid";
    public string Audience { get; set; } = "geogrid";
    public string Key { get; set; } = string.Empty;
    public int ExpiresMinutes { get; set; } = 60 * 24 * 7;
}
