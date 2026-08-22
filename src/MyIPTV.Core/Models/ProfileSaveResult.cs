namespace MyIPTV.Core.Models;

public sealed record ProfileSaveResult(bool IsSuccess, string Message, IptvProfile? Profile)
{
    public static ProfileSaveResult Success(IptvProfile profile) =>
        new(true, "Profile saved.", profile);

    public static ProfileSaveResult Failure(string message) =>
        new(false, message, null);
}
