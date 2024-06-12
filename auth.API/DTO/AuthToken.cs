namespace auth.API.DTO;

public class AuthToken
{
    public string Token { get; set; } = string.Empty;
    public bool Result { get; set; }
    public List<string> Errors { get; set; }
}