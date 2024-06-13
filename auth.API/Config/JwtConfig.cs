namespace auth.API.Config;

public class JwtConfig
{
    public string Secret { get; set; } = string.Empty;
    public TimeSpan ExpiryTimeFrame { get; set; }
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
}