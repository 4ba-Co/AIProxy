namespace AIProxy.Common;

public sealed class UserRequestToken
{
    public string Id { get; set; } = null!;
    public string Token { get; set; } = null!;
    public DateTime ExpiredAt { get; set; }
}