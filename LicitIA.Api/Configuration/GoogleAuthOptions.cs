namespace LicitIA.Api.Configuration;

public sealed class GoogleAuthOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = "http://localhost:5153/api/auth/google/callback";
}
