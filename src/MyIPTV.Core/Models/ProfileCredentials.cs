namespace MyIPTV.Core.Models;

public sealed class ProfileCredentials(string? username, string password)
{
    public string? Username { get; } = username;

    public string Password { get; } = password;

    public override string ToString() => "ProfileCredentials { Username = [REDACTED], Password = [REDACTED] }";
}
